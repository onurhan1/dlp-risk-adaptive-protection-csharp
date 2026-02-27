using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// DLP API'den "Released quarantined message" incident'larını çekip veritabanına kaydeden servis.
/// Manuel/on-demand tetikleme içindir (POST /api/released-incidents/sync).
/// Otomatik akış Collector → Redis (dlp:released-incidents) → Analyzer (DatabaseService) üzerinden gider.
/// </summary>
public class ReleasedIncidentSyncService
{
    private readonly AnalyzerDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReleasedIncidentSyncService> _logger;

    private static readonly string[] DateFormats = {
        "dd/MM/yyyy HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "MM/dd/yyyy HH:mm:ss",
        "dd-MM-yyyy HH:mm:ss"
    };

    public ReleasedIncidentSyncService(
        AnalyzerDbContext context,
        IConfiguration configuration,
        ILogger<ReleasedIncidentSyncService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// DLP API'den released incident'ları çeker ve veritabanına kaydeder.
    /// Varsayılan olarak son 24 saatlik veriyi tarar.
    /// </summary>
    public async Task<ReleasedSyncResult> SyncAsync(int lookbackHours = 24)
    {
        var result = new ReleasedSyncResult();
        HttpClient? httpClient = null;

        try
        {
            // Read DLP API credentials directly from appsettings.json (same as Collector)
            var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
            var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
            var useHttps = _configuration.GetValue<bool>("DLP:UseHttps", true);
            var username = _configuration["DLP:Username"] ?? string.Empty;
            var password = _configuration["DLP:Password"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                dlpIp == "YOUR_DLP_MANAGER_IP" || username == "YOUR_DLP_USERNAME")
            {
                result.ErrorMessage = "DLP API ayarları appsettings.json'da yapılandırılmamış";
                _logger.LogWarning("Released incident sync atlandı: DLP API ayarları appsettings.json'da yapılandırılmamış");
                return result;
            }

            httpClient = CreateHttpClient(dlpIp, dlpPort, useHttps);

            var accessToken = await AuthenticateAsync(httpClient, username, password);
            if (string.IsNullOrEmpty(accessToken))
            {
                result.ErrorMessage = "DLP API kimlik doğrulama başarısız";
                _logger.LogError("Released incident sync: Authentication failed");
                return result;
            }

            var endTime = DateTime.Now;
            var startTime = endTime.AddHours(-lookbackHours);

            _logger.LogInformation(
                "Released incident sync başlıyor: {StartTime} -> {EndTime} ({LookbackHours}h lookback)",
                startTime, endTime, lookbackHours);

            var allIncidents = await FetchIncidentsWithHistoryAsync(httpClient, accessToken, startTime, endTime);

            if (allIncidents.Count == 0)
            {
                _logger.LogInformation("Released incident sync: Belirtilen tarih aralığında incident bulunamadı");
                return result;
            }

            _logger.LogInformation("Released incident sync: {Count} incident çekildi, released olanlar aranıyor...", allIncidents.Count);

            var releasedEntries = ExtractReleasedIncidents(allIncidents);

            if (releasedEntries.Count == 0)
            {
                _logger.LogInformation("Released incident sync: 'Released quarantined message' kaydı bulunamadı");
                return result;
            }

            _logger.LogInformation("Released incident sync: {Count} released incident bulundu, veritabanına kaydediliyor...", releasedEntries.Count);

            var (inserted, skipped) = await SaveToDatabase(releasedEntries);

            result.TotalFetched = allIncidents.Count;
            result.ReleasedFound = releasedEntries.Count;
            result.Inserted = inserted;
            result.Skipped = skipped;
            result.Success = true;

            _logger.LogInformation(
                "Released incident sync tamamlandı: {Fetched} incident tarandı, {Found} released bulundu, {Inserted} eklendi, {Skipped} zaten vardı",
                allIncidents.Count, releasedEntries.Count, inserted, skipped);

            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            result.ErrorMessage = "DLP API ayarları yapılandırılmamış";
            _logger.LogWarning("Released incident sync atlandı: DLP API yapılandırılmamış. {Message}", ex.Message);
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Released incident sync başarısız");
            return result;
        }
        finally
        {
            httpClient?.Dispose();
        }
    }

    private async Task<string?> AuthenticateAsync(HttpClient httpClient, string username, string password)
    {
        try
        {
            var authRequest = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
            authRequest.Headers.Add("username", username);
            authRequest.Headers.Add("password", password);

            var authResponse = await httpClient.SendAsync(authRequest);
            if (!authResponse.IsSuccessStatusCode)
            {
                var error = await authResponse.Content.ReadAsStringAsync();
                _logger.LogError("Released incident sync: Auth failed. Status: {Status}, Error: {Error}",
                    authResponse.StatusCode, error);
                return null;
            }

            var authContent = await authResponse.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(authContent);

            return tokenResponse?.ContainsKey("access_token") == true
                ? tokenResponse["access_token"].ToString()
                : tokenResponse?.ContainsKey("accessToken") == true
                    ? tokenResponse["accessToken"].ToString()
                    : tokenResponse?.ContainsKey("token") == true
                        ? tokenResponse["token"].ToString()
                        : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Released incident sync: Authentication exception");
            return null;
        }
    }

    /// <summary>
    /// DLP API'den incident'ları 3 günlük chunk'lar halinde çeker (API limiti).
    /// History alanı dahil ham JSON olarak döndürür.
    /// </summary>
    private async Task<List<JsonElement>> FetchIncidentsWithHistoryAsync(
        HttpClient httpClient, string accessToken, DateTime startTime, DateTime endTime)
    {
        var allIncidents = new List<JsonElement>();
        var chunkSize = TimeSpan.FromDays(3);
        var currentStart = startTime;
        int chunkNumber = 0;

        while (currentStart < endTime)
        {
            chunkNumber++;
            var currentEnd = currentStart.Add(chunkSize);
            if (currentEnd > endTime) currentEnd = endTime;

            var fromDateStr = currentStart.ToString("dd/MM/yyyy HH:mm:ss");
            var toDateStr = currentEnd.ToString("dd/MM/yyyy HH:mm:ss");

            _logger.LogDebug("Released sync chunk {ChunkNumber}: {Start} -> {End}", chunkNumber, fromDateStr, toDateStr);

            var requestBody = JsonSerializer.Serialize(new
            {
                type = "INCIDENTS",
                from_date = fromDateStr,
                to_date = toDateStr,
                limit = 10000
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/incidents");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Released sync chunk {ChunkNumber}: HTTP {Status}", chunkNumber, response.StatusCode);
                    currentStart = currentEnd;
                    continue;
                }

                var responseString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(responseString))
                {
                    currentStart = currentEnd;
                    continue;
                }

                var chunkIncidents = ParseIncidentsArray(responseString);
                _logger.LogDebug("Released sync chunk {ChunkNumber}: {Count} incidents", chunkNumber, chunkIncidents.Count);

                allIncidents.AddRange(chunkIncidents);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Released sync chunk {ChunkNumber} başarısız, devam ediliyor", chunkNumber);
            }

            currentStart = currentEnd;
            await Task.Delay(500);
        }

        return allIncidents;
    }

    private List<JsonElement> ParseIncidentsArray(string responseString)
    {
        var results = new List<JsonElement>();

        try
        {
            using var doc = JsonDocument.Parse(responseString);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                    results.Add(item.Clone());
            }
            else if (doc.RootElement.TryGetProperty("incidents", out var incidentsArr) &&
                     incidentsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in incidentsArr.EnumerateArray())
                    results.Add(item.Clone());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Released sync: JSON parse hatası");
        }

        return results;
    }

    /// <summary>
    /// Her incident'ın history dizisinde "Released quarantined message" task_name'ini arar.
    /// </summary>
    private List<ReleasedIncident> ExtractReleasedIncidents(List<JsonElement> incidents)
    {
        var releasedEntries = new List<ReleasedIncident>();

        foreach (var incident in incidents)
        {
            if (!incident.TryGetProperty("history", out var history) ||
                history.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var historyItem in history.EnumerateArray())
            {
                var taskName = historyItem.TryGetProperty("task_name", out var tn) ? tn.GetString() : null;
                if (taskName != "Released quarantined message")
                    continue;

                var incidentId = incident.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;
                var incidentTime = incident.TryGetProperty("incident_time", out var itProp) ? itProp.GetString() : null;
                var action = incident.TryGetProperty("action", out var actProp) ? actProp.GetString() : "";
                var adminName = historyItem.TryGetProperty("admin_name", out var anProp) ? anProp.GetString() : null;
                var comments = historyItem.TryGetProperty("comments", out var cmProp) ? cmProp.GetString() : null;
                var updateTime = historyItem.TryGetProperty("update_time", out var utProp) ? utProp.GetString() : null;

                DateTime? incidentTimestamp = TryParseDate(incidentTime);
                DateTime? updateTimestamp = TryParseDate(updateTime);

                releasedEntries.Add(new ReleasedIncident
                {
                    IncidentId = incidentId,
                    IncidentTimestamp = incidentTimestamp ?? DateTime.MinValue,
                    Action = action ?? "",
                    TaskName = taskName,
                    AdminName = adminName,
                    Comments = comments,
                    UpdateTime = updateTimestamp
                });
            }
        }

        return releasedEntries;
    }

    private DateTime? TryParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
            return null;

        foreach (var format in DateFormats)
        {
            if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
                return parsed;
        }

        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
            return fallback;

        return null;
    }

    private async Task<(int inserted, int skipped)> SaveToDatabase(List<ReleasedIncident> entries)
    {
        int inserted = 0;
        int skipped = 0;

        foreach (var entry in entries)
        {
            try
            {
                var exists = await _context.ReleasedIncidents
                    .AnyAsync(r => r.IncidentId == entry.IncidentId && r.UpdateTime == entry.UpdateTime);

                if (exists)
                {
                    skipped++;
                    continue;
                }

                _context.ReleasedIncidents.Add(entry);
                await _context.SaveChangesAsync();
                inserted++;
            }
            catch (DbUpdateException)
            {
                skipped++;
                _context.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Released incident kaydetme hatası: IncidentId={IncidentId}", entry.IncidentId);
                _context.ChangeTracker.Clear();
            }
        }

        return (inserted, skipped);
    }

    private HttpClient CreateHttpClient(string dlpIp, int dlpPort, bool useHttps)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        var baseUrl = useHttps
            ? $"https://{dlpIp}:{dlpPort}/"
            : $"http://{dlpIp}:{dlpPort}/";

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }
}

public class ReleasedSyncResult
{
    public bool Success { get; set; }
    public int TotalFetched { get; set; }
    public int ReleasedFound { get; set; }
    public int Inserted { get; set; }
    public int Skipped { get; set; }
    public string? ErrorMessage { get; set; }
}
