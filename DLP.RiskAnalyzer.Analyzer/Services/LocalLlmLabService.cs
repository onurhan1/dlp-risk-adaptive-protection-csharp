using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Controlled bridge between RADAR incidents and an Ollama-compatible local model.
/// The model receives a bounded, read-only snapshot rather than database credentials or SQL.
/// </summary>
public sealed class LocalLlmLabService : ILocalLlmLabService
{
    private const string EnabledKey = "local_llm_lab_enabled";
    private const string GenerateUrlKey = "local_llm_lab_generate_url";
    private const string ModelKey = "local_llm_lab_model";
    private const string TemperatureKey = "local_llm_lab_temperature";
    private const string MaxTokensKey = "local_llm_lab_max_tokens";

    private readonly AnalyzerDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalLlmLabService> _logger;

    public LocalLlmLabService(
        AnalyzerDbContext context,
        HttpClient httpClient,
        ILogger<LocalLlmLabService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LocalLlmLabSettings> GetSettingsAsync(CancellationToken ct)
    {
        var values = await _context.SystemSettings.AsNoTracking()
            .Where(setting => setting.Key.StartsWith("local_llm_lab_"))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, ct);

        return new LocalLlmLabSettings(
            bool.TryParse(values.GetValueOrDefault(EnabledKey), out var enabled) && enabled,
            values.GetValueOrDefault(GenerateUrlKey, "http://127.0.0.1:11434/api/generate"),
            values.GetValueOrDefault(ModelKey, "qwen3:8b"),
            double.TryParse(values.GetValueOrDefault(TemperatureKey), out var temperature) ? temperature : 0.2,
            int.TryParse(values.GetValueOrDefault(MaxTokensKey), out var maxTokens) ? maxTokens : 1800);
    }

    public async Task SaveSettingsAsync(LocalLlmLabSettings settings, CancellationToken ct)
    {
        ValidateSettings(settings);
        var entries = new Dictionary<string, string>
        {
            [EnabledKey] = settings.Enabled.ToString(),
            [GenerateUrlKey] = settings.GenerateUrl.Trim(),
            [ModelKey] = settings.Model.Trim(),
            [TemperatureKey] = settings.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MaxTokensKey] = settings.MaxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        var existing = await _context.SystemSettings
            .Where(setting => entries.Keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, ct);

        foreach (var (key, value) in entries)
        {
            if (existing.TryGetValue(key, out var setting)) setting.Value = value;
            else _context.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<string> TestConnectionAsync(LocalLlmLabSettings settings, CancellationToken ct)
    {
        ValidateSettings(settings);
        var reply = await GenerateAsync(settings, "RADAR bağlantı testi. Yalnızca 'bağlantı başarılı' yaz.", 48, ct);
        return string.IsNullOrWhiteSpace(reply) ? "Yerel model boş yanıt verdi." : reply.Trim();
    }

    public async Task<LocalLlmIncidentSnapshot> GetIncidentSnapshotAsync(LocalLlmSnapshotRequest request, CancellationToken ct)
    {
        var lookbackDays = Math.Clamp(request.LookbackDays, 1, 180);
        var sampleSize = Math.Clamp(request.SampleSize, 5, 100);
        var end = DateTime.UtcNow;
        var start = end.AddDays(-lookbackDays);
        var incidents = _context.Incidents.AsNoTracking().Where(incident => incident.Timestamp >= start && incident.Timestamp <= end);

        var total = await incidents.CountAsync(ct);
        var uniqueUsers = await incidents.Select(incident => incident.UserEmail).Distinct().CountAsync(ct);
        var maximumMatches = await incidents.MaxAsync(incident => (int?)incident.MaxMatches, ct) ?? 0;

        var actions = await CountByAsync(incidents.Select(incident => incident.Action), ct);
        var channels = await CountByAsync(incidents.Select(incident => incident.Channel), ct);
        var policies = await CountByAsync(incidents.Select(incident => incident.Policy ?? incident.RuleName), ct);

        // PostgreSQL'in çevirebildiği anonim projeksiyon ile sorguyu bitir; maskeleme
        // ve LocalLlm DTO üretimi EF sorgusunun dışında yapılır.
        var userRows = await incidents
            .GroupBy(incident => new { incident.UserEmail, incident.Department })
            .Select(group => new
            {
                User = group.Key.UserEmail,
                group.Key.Department,
                IncidentCount = group.Count(),
                MaximumSeverity = group.Max(incident => incident.Severity),
                MaximumMatches = group.Max(incident => incident.MaxMatches),
                AverageDataSensitivity = group.Average(incident => (double)incident.DataSensitivity),
                RepeatCount = group.Sum(incident => incident.RepeatCount),
            })
            .OrderByDescending(user => user.MaximumMatches)
            .ThenByDescending(user => user.IncidentCount)
            .Take(30)
            .ToListAsync(ct);
        var users = userRows.Select(user => new LocalLlmUserProfile(
            request.MaskIdentifiers ? Mask(user.User) : user.User,
            user.Department,
            user.IncidentCount,
            user.MaximumSeverity,
            user.MaximumMatches,
            user.AverageDataSensitivity,
            user.RepeatCount)).ToList();

        var sampleRows = await incidents
            .OrderByDescending(incident => incident.MaxMatches)
            .ThenByDescending(incident => incident.Severity)
            .ThenByDescending(incident => incident.RepeatCount)
            .ThenByDescending(incident => incident.Timestamp)
            .Take(sampleSize)
            .Select(incident => new
            {
                incident.Timestamp,
                incident.UserEmail,
                incident.Department,
                incident.Action,
                incident.Severity,
                incident.MaxMatches,
                incident.DataSensitivity,
                incident.RepeatCount,
                incident.Policy,
                incident.RuleName,
                incident.Channel,
                incident.Destination,
            })
            .ToListAsync(ct);
        var samples = sampleRows.Select(incident => new LocalLlmIncidentSample(
            incident.Timestamp,
            request.MaskIdentifiers ? Mask(incident.UserEmail) : incident.UserEmail,
            incident.Department,
            incident.Action,
            incident.Severity,
            incident.MaxMatches,
            incident.DataSensitivity,
            incident.RepeatCount,
            incident.Policy,
            incident.RuleName,
            incident.Channel,
            request.MaskIdentifiers ? MaskDestination(incident.Destination) : incident.Destination)).ToList();

        return new LocalLlmIncidentSnapshot(
            start, end, total, uniqueUsers, maximumMatches,
            actions, channels, policies, users, samples);
    }

    public async Task<LocalLlmChatResult> ChatAsync(LocalLlmChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Mesaj boş olamaz.", nameof(request));

        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled)
            throw new InvalidOperationException("Yerel LLM Laboratuvarı ayarlardan etkinleştirilmemiş.");

        var snapshot = await GetIncidentSnapshotAsync(
            new LocalLlmSnapshotRequest(request.LookbackDays, request.SampleSize, request.MaskIdentifiers), ct);
        var prompt = BuildPrompt(request, snapshot);
        var reply = await GenerateAsync(settings, prompt, settings.MaxTokens, ct);
        return new LocalLlmChatResult(reply, snapshot);
    }

    private async Task<IReadOnlyList<LocalLlmCount>> CountByAsync(IQueryable<string?> source, CancellationToken ct)
    {
        var rows = await source
            .Where(value => value != null && value != "")
            .GroupBy(value => value!)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .Take(12)
            .ToListAsync(ct);

        return rows.Select(row => new LocalLlmCount(row.Name, row.Count)).ToList();
    }

    private async Task<string> GenerateAsync(LocalLlmLabSettings settings, string prompt, int maxTokens, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.GenerateUrl)
        {
            Content = JsonContent.Create(new
            {
                model = settings.Model,
                prompt,
                stream = false,
                options = new { temperature = settings.Temperature, num_predict = Math.Clamp(maxTokens, 64, 4096) },
            }),
        };
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Local LLM returned {StatusCode}: {Body}", response.StatusCode, body[..Math.Min(body.Length, 800)]);
            throw new InvalidOperationException($"Yerel LLM isteği başarısız oldu ({(int)response.StatusCode}).");
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("response", out var responseText)
            ? responseText.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string BuildPrompt(LocalLlmChatRequest request, LocalLlmIncidentSnapshot snapshot)
    {
        var history = (request.History ?? [])
            .Where(message => message.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(message.Content))
            .TakeLast(10)
            .Select(message => $"{(message.Role == "user" ? "Kullanıcı" : "Asistan")}: {message.Content.Trim()[..Math.Min(message.Content.Trim().Length, 3000)]}");
        var context = FormatSnapshot(snapshot);

        return $$"""
Sen RADAR içindeki yerel DLP risk modelleme laboratuvarının analist asistanısın.
Kullanıcıyla doğal, kısa ve yardımcı bir Türkçe sohbet yürüt. Selamlaşmaya selamlaşarak karşılık ver;
her mesajda risk algoritması üretme. Yalnızca kullanıcı risk puanlama, sınıflandırma veya incident analizi isterse
bu konulara geç. Sana yalnızca salt-okunur incident özeti verildi; bu veride olmayan olguları kesinmiş gibi iddia etme.
RADAR'ın mevcut risk_score alanı bu bağlama bilinçli olarak dahil edilmemiştir; onu kullanma veya varsayma.
Bir risk puanlama algoritması istendiğinde 0-100 aralığı, ölçülebilir faktörler, ağırlık toplamı, eşikler,
doğrulama adımları ve sınırlılıkları öner. Kullanıcı sınıflandırmasını yalnızca ham olay özellikleri,
yoğunluk, tekrar, max match, şiddet, veri hassasiyeti, aksiyon ve dağılımlardan türet.
Üretim kuralı değiştirme, kullanıcıya işlem uygulama veya SQL üretme.
Yanıtlarını Türkçe, denetlenebilir ve kısa başlıklarla yaz. Kullanıcı açıkça JSON istemedikçe JSON,
JSON şeması, kod bloğu veya yalnızca yapılandırılmış veri döndürme.

INCIDENT BAĞLAMI (yalnızca ilgili olduğunda kullan):
{{context}}

{{string.Join("\n", history)}}
Kullanıcı: {{request.Message.Trim()}}
Asistan:
""";
    }

    private static string FormatSnapshot(LocalLlmIncidentSnapshot snapshot)
    {
        var text = new StringBuilder()
            .AppendLine($"Dönem: {snapshot.StartUtc:yyyy-MM-dd} - {snapshot.EndUtc:yyyy-MM-dd} (UTC)")
            .AppendLine($"Toplam olay: {snapshot.TotalIncidents}; benzersiz kullanıcı: {snapshot.UniqueUsers}; en yüksek maximum match: {snapshot.MaximumMatches}.")
            .AppendLine($"Aksiyon dağılımı: {FormatCounts(snapshot.Actions)}")
            .AppendLine($"Kanal dağılımı: {FormatCounts(snapshot.Channels)}")
            .AppendLine($"Politika dağılımı: {FormatCounts(snapshot.Policies)}")
            .AppendLine("Öne çıkan kullanıcı profilleri:");

        foreach (var user in snapshot.Users)
        {
            text.AppendLine($"- Kullanıcı: {user.User}; birim: {user.Department ?? "-"}; olay: {user.IncidentCount}; " +
                $"max match: {user.MaximumMatches}; en yüksek şiddet: {user.MaximumSeverity}; tekrar: {user.RepeatCount}; " +
                $"ortalama veri hassasiyeti: {user.AverageDataSensitivity:F1}.");
        }

        text.AppendLine("Örnek olaylar:");
        foreach (var incident in snapshot.Samples)
        {
            text.AppendLine($"- {incident.Timestamp:yyyy-MM-dd HH:mm}; kullanıcı: {incident.User}; aksiyon: {incident.Action ?? "-"}; " +
                $"max match: {incident.MaxMatches}; şiddet: {incident.Severity}; tekrar: {incident.RepeatCount}; " +
                $"politika: {incident.Policy ?? "-"}; kural: {incident.Rule ?? "-"}; kanal: {incident.Channel ?? "-"}; hedef: {incident.Destination ?? "-"}.");
        }

        return text.ToString();
    }

    private static string FormatCounts(IReadOnlyList<LocalLlmCount> counts) =>
        counts.Count == 0 ? "veri yok" : string.Join(", ", counts.Select(item => $"{item.Name}: {item.Count}"));

    private static void ValidateSettings(LocalLlmLabSettings settings)
    {
        if (!Uri.TryCreate(settings.GenerateUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Geçerli bir http/https generate URL girin.");
        if (string.IsNullOrWhiteSpace(settings.Model)) throw new ArgumentException("Model adı zorunludur.");
        if (settings.Temperature is < 0 or > 2) throw new ArgumentException("Sıcaklık 0 ile 2 arasında olmalıdır.");
        if (settings.MaxTokens is < 64 or > 4096) throw new ArgumentException("Maksimum çıktı 64 ile 4096 arasında olmalıdır.");
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var at = value.IndexOf('@');
        if (at > 0) return $"{value[..1]}***{value[at..]}";
        return value.Length <= 3 ? "***" : $"{value[..2]}***";
    }

    private static string? MaskDestination(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var at = value.LastIndexOf('@');
        return at > 0 ? $"***{value[at..]}" : value;
    }
}
