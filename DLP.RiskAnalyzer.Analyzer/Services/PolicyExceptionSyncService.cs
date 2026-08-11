using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// DLP API'den policy rule exception bilgilerini çekip veritabanına kaydeden servis.
/// 24 saatte bir senkronize edilir.
/// </summary>
public class PolicyExceptionSyncService : IPolicyExceptionSyncService
{
    private readonly AnalyzerDbContext _context;
    private readonly IDlpConfigurationService _dlpConfigService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PolicyExceptionSyncService> _logger;

    // In-memory cache (thread-safe)
    private static Dictionary<string, string> _exceptionLookupCache = new();
    private static DateTime _lastCacheRefresh = DateTime.MinValue;

    public PolicyExceptionSyncService(
        AnalyzerDbContext context,
        IDlpConfigurationService dlpConfigService,
        IConfiguration configuration,
        ILogger<PolicyExceptionSyncService> logger)
    {
        _context = context;
        _dlpConfigService = dlpConfigService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// DLP API'den exception bilgilerini çeker ve veritabanına kaydeder.
    /// </summary>
    public async Task<int> SyncAsync()
    {
        HttpClient? httpClient = null;
        try
        {
            httpClient = await CreateHttpClientAsync();
            var config = await _dlpConfigService.GetSensitiveConfigAsync();

            // Step 1: Authenticate
            var authRequest = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
            authRequest.Headers.Add("username", config.Username);
            authRequest.Headers.Add("password", config.Password);

            var authResponse = await httpClient.SendAsync(authRequest);
            if (!authResponse.IsSuccessStatusCode)
            {
                var error = await authResponse.Content.ReadAsStringAsync();
                _logger.LogError("Policy exception sync: Authentication failed. Status: {Status}, Error: {Error}",
                    authResponse.StatusCode, error);
                return 0;
            }

            var authContent = await authResponse.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(authContent);
            var accessToken = tokenResponse?.ContainsKey("access_token") == true
                ? tokenResponse["access_token"].ToString()
                : tokenResponse?.ContainsKey("accessToken") == true
                    ? tokenResponse["accessToken"].ToString()
                    : tokenResponse?.ContainsKey("token") == true
                        ? tokenResponse["token"].ToString()
                        : null;

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Policy exception sync: No access token received");
                return 0;
            }

            // Step 2: Fetch all policy rules exceptions (type=DLP)
            var exceptionsUrl = "/dlp/rest/v1/policy/rules/exceptions/all?type=DLP";
            _logger.LogInformation("Fetching policy exceptions from: {Url}", exceptionsUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, exceptionsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Policy exception sync: Failed to fetch. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return 0;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var exceptions = ParseExceptions(responseContent);
            await EnrichExceptionStatusesAsync(httpClient, accessToken, exceptions);

            if (exceptions.Count == 0)
            {
                _logger.LogInformation("Policy exception sync: No exceptions found in DLP API response");
                return 0;
            }

            // Step 3: Save to database (replace all)
            var syncedCount = await SaveExceptionsAsync(exceptions);
            
            // Step 4: Refresh in-memory cache
            await RefreshCacheAsync();

            _logger.LogInformation("Policy exception sync completed: {Count} exceptions saved", syncedCount);
            return syncedCount;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            _logger.LogWarning("Policy exception sync skipped: DLP API not configured. {Message}", ex.Message);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Policy exception sync failed");
            return 0;
        }
        finally
        {
            httpClient?.Dispose();
        }
    }

    /// <summary>
    /// (policy_name|exception_name) → Parent rule name lookup döndürür.
    /// Önce in-memory cache, yoksa DB'den yükler.
    /// </summary>
    public async Task<Dictionary<string, string>> GetExceptionLookupAsync()
    {
        // Cache 1 saat geçerli
        if (_exceptionLookupCache.Count > 0 && (DateTime.UtcNow - _lastCacheRefresh).TotalHours < 1)
        {
            return _exceptionLookupCache;
        }

        await RefreshCacheAsync();
        return _exceptionLookupCache;
    }

    /// <summary>
    /// Verilen policy + rule name'in exception olup olmadığını kontrol eder.
    /// </summary>
    public async Task<bool> IsExceptionAsync(string policyName, string ruleName)
    {
        var lookup = await GetExceptionLookupAsync();
        var key = $"{policyName}|{ruleName}";
        return lookup.ContainsKey(key);
    }

    private async Task RefreshCacheAsync()
    {
        try
        {
            var allExceptions = await _context.PolicyRuleExceptions
                .AsNoTracking()
                .ToListAsync();

            // Composite key: "policy_name|exception_name" → parent rule_name
            var lookup = new Dictionary<string, string>();
            foreach (var e in allExceptions)
            {
                var key = $"{e.PolicyName}|{e.ExceptionName}";
                lookup[key] = e.RuleName; // Last-write-wins if duplicates exist
            }

            _exceptionLookupCache = lookup;
            _lastCacheRefresh = DateTime.UtcNow;
            _logger.LogInformation("Policy exception cache refreshed: {Count} entries", lookup.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh policy exception cache");
        }
    }

    /// <summary>
    /// DLP API response JSON'ından exception bilgilerini parse eder.
    /// </summary>
    private List<PolicyRuleException> ParseExceptions(string responseJson)
    {
        var exceptions = new List<PolicyRuleException>();
        try
        {
            var jsonDoc = JsonDocument.Parse(responseJson);
            var root = jsonDoc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("data", out var data)) root = data;
                else if (root.TryGetProperty("items", out var items)) root = items;
                else if (root.TryGetProperty("result", out var result)) root = result;
            }

            JsonElement exceptionRules;
            if (root.ValueKind == JsonValueKind.Array)
            {
                exceptionRules = root;
            }
            else if (!TryGetProperty(root, out exceptionRules, "exception_rules", "exceptionRules", "rules", "policy_rules"))
            {
                return exceptions;
            }

            if (exceptionRules.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in exceptionRules.EnumerateArray())
                {
                    var policyName = GetString(rule, "policy_name", "PolicyName", "policyName") ?? "";
                    var ruleName = GetString(rule, "parent_rule_name", "parentRuleName", "rule_name", "RuleName", "ruleName", "name") ?? "";

                    if (TryGetProperty(rule, out var ern, "exception_rule_names", "exceptionRuleNames", "exception_rules", "exceptions") &&
                        ern.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var exceptionNameElem in ern.EnumerateArray())
                        {
                            var exceptionName = exceptionNameElem.ValueKind == JsonValueKind.String
                                ? exceptionNameElem.GetString()
                                : GetString(exceptionNameElem, "exception_rule_name", "exceptionRuleName", "exception_name", "exceptionName", "rule_name", "RuleName", "name");
                            var enabled = exceptionNameElem.ValueKind == JsonValueKind.Object
                                ? GetBooleanString(exceptionNameElem, "enabled", "Enabled", "is_enabled", "isEnabled") ?? "true"
                                : "true";
                            if (!string.IsNullOrEmpty(exceptionName))
                            {
                                exceptions.Add(new PolicyRuleException
                                {
                                    PolicyName = policyName,
                                    RuleName = ruleName,
                                    ExceptionName = exceptionName,
                                    Enabled = enabled,
                                    SyncedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse policy exceptions from API response");
        }

        return exceptions;

        static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
                    return true;
            }

            value = default;
            return false;
        }

        static string? GetString(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }

            return null;
        }

        static string? GetBooleanString(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value)) continue;
                if (value.ValueKind == JsonValueKind.String) return value.GetString()?.ToLowerInvariant();
                if (value.ValueKind == JsonValueKind.True) return "true";
                if (value.ValueKind == JsonValueKind.False) return "false";
            }

            return null;
        }
    }

    /// <summary>
    /// Exception'ları veritabanına kaydeder (mevcut kayıtları temizleyip yenilerini ekler).
    /// </summary>
    private static Dictionary<string, string> ParseExceptionStatuses(string responseJson)
    {
        var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseJson);
            VisitExceptionStatusNodes(jsonDoc.RootElement, statuses);
        }
        catch
        {
            return statuses;
        }

        return statuses;
    }

    private static void VisitExceptionStatusNodes(JsonElement element, Dictionary<string, string> statuses)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var exceptionName = GetJsonString(element, "exception_rule_name", "exceptionRuleName", "exception_name", "exceptionName");
            var enabled = GetJsonBooleanString(element, "enabled", "Enabled", "is_enabled", "isEnabled");
            if (!string.IsNullOrWhiteSpace(exceptionName) && !string.IsNullOrWhiteSpace(enabled))
            {
                statuses[exceptionName] = enabled;
            }

            foreach (var property in element.EnumerateObject())
            {
                VisitExceptionStatusNodes(property.Value, statuses);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                VisitExceptionStatusNodes(item, statuses);
            }
        }
    }

    private static string? GetJsonString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
        }

        return null;
    }

    private static string? GetJsonBooleanString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString()?.ToLowerInvariant();
            if (value.ValueKind == JsonValueKind.True) return "true";
            if (value.ValueKind == JsonValueKind.False) return "false";
        }

        return null;
    }

    private async Task<(bool Success, System.Net.HttpStatusCode? Status, string Body)> FetchExceptionDetailAsync(
        HttpClient httpClient,
        string accessToken,
        string ruleName)
    {
        var encodedRuleName = Uri.EscapeDataString(ruleName);
        var formEncodedRuleName = System.Net.WebUtility.UrlEncode(ruleName);
        var paths = new[]
            {
                $"/dlp/rest/v1/policy/rules/exceptions?type=DLP&ruleName={encodedRuleName}",
                $"/dlp/rest/v1/policy/rules/exceptions?ruleName={encodedRuleName}&type=DLP",
                $"/dlp/rest/v1/policy/rules/exceptions?type=DLP&ruleName={formEncodedRuleName}"
            }
            .Distinct(StringComparer.Ordinal)
            .ToList();

        System.Net.HttpStatusCode? lastStatus = null;
        var lastBody = string.Empty;

        foreach (var path in paths)
        {
            var result = await SendExceptionDetailRequestAsync(httpClient, accessToken, path, includeBody: false);
            if (result.Success) return result;
            lastStatus = result.Status;
            lastBody = result.Body;
            _logger.LogDebug(
                "Policy exception sync: detail fetch attempt failed. Rule={RuleName}, Path={Path}, Status={Status}",
                ruleName, path, result.Status);
        }

        foreach (var path in paths)
        {
            var result = await SendExceptionDetailRequestAsync(httpClient, accessToken, path, includeBody: true);
            if (result.Success) return result;
            lastStatus = result.Status;
            lastBody = result.Body;
            _logger.LogDebug(
                "Policy exception sync: detail fetch attempt with body failed. Rule={RuleName}, Path={Path}, Status={Status}",
                ruleName, path, result.Status);
        }

        return (false, lastStatus, lastBody);
    }

    private static async Task<(bool Success, System.Net.HttpStatusCode? Status, string Body)> SendExceptionDetailRequestAsync(
        HttpClient httpClient,
        string accessToken,
        string path,
        bool includeBody)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (includeBody)
        {
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        using var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.IsSuccessStatusCode, response.StatusCode, body);
    }

    private async Task EnrichExceptionStatusesAsync(HttpClient httpClient, string accessToken, List<PolicyRuleException> exceptions)
    {
        var ruleNames = exceptions
            .Select(e => e.RuleName)
            .Where(ruleName => !string.IsNullOrWhiteSpace(ruleName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var ruleName in ruleNames)
        {
            try
            {
                var detailResult = await FetchExceptionDetailAsync(httpClient, accessToken, ruleName);
                if (!detailResult.Success)
                {
                    _logger.LogWarning(
                        "Policy exception sync: detail fetch failed. Rule={RuleName}, Status={Status}, Body={Body}",
                        ruleName, detailResult.Status, detailResult.Body);
                    continue;
                }

                var statusLookup = ParseExceptionStatuses(detailResult.Body);
                if (statusLookup.Count == 0)
                {
                    _logger.LogWarning("Policy exception sync: no enabled status found in detail response. Rule={RuleName}", ruleName);
                    continue;
                }

                foreach (var exception in exceptions.Where(e => string.Equals(e.RuleName, ruleName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (statusLookup.TryGetValue(exception.ExceptionName, out var enabled))
                    {
                        exception.Enabled = enabled;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Policy exception sync: failed to enrich enabled statuses. Rule={RuleName}", ruleName);
            }
        }
    }

    private async Task<int> SaveExceptionsAsync(List<PolicyRuleException> exceptions)
    {
        // Mevcut kayıtları sil
        var existing = await _context.PolicyRuleExceptions.ToListAsync();
        _context.PolicyRuleExceptions.RemoveRange(existing);
        await _context.SaveChangesAsync();

        // Yeni kayıtları ekle
        _context.PolicyRuleExceptions.AddRange(exceptions);
        await _context.SaveChangesAsync();

        return exceptions.Count;
    }

    /// <summary>
    /// DLP API için HttpClient oluşturur.
    /// </summary>
    private async Task<HttpClient> CreateHttpClientAsync()
    {
        try
        {
            var config = await _dlpConfigService.GetSensitiveConfigAsync();

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            var baseUrl = config.UseHttps
                ? $"https://{config.ManagerIp}:{config.ManagerPort}/"
                : $"http://{config.ManagerIp}:{config.ManagerPort}/";

            return new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load DLP config from database, falling back to appsettings.json");
            var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
            var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
            var useHttps = _configuration.GetValue<bool>("DLP:UseHttps", true);
            var timeout = _configuration.GetValue<int>("DLP:Timeout", 30);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            var baseUrl = useHttps
                ? $"https://{dlpIp}:{dlpPort}/"
                : $"http://{dlpIp}:{dlpPort}/";

            return new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(timeout)
            };
        }
    }
}
