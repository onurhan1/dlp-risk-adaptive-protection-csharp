using System.Net.Http.Json;
using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Supplies a local model with a bounded RADAR capability and coverage snapshot.
/// The model is advisory only: this service exposes no write-capable tool.
/// </summary>
public sealed class SecurityAgentService : ISecurityAgentService
{
    private const string EnabledKey = "local_llm_lab_enabled";
    private const string GenerateUrlKey = "local_llm_lab_generate_url";
    private const string ModelKey = "local_llm_lab_model";
    private const string TemperatureKey = "local_llm_lab_temperature";
    private const string MaxTokensKey = "local_llm_lab_max_tokens";

    private readonly AnalyzerDbContext _context;
    private readonly IPlaybookEngine _playbookEngine;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SecurityAgentService> _logger;

    public SecurityAgentService(AnalyzerDbContext context, IPlaybookEngine playbookEngine, HttpClient httpClient, ILogger<SecurityAgentService> logger)
    {
        _context = context;
        _playbookEngine = playbookEngine;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SecurityAgentContext> GetContextAsync(SecurityAgentContextRequest request, CancellationToken ct)
    {
        await PlaybookSchema.EnsureAsync(_context, _logger, ct);

        var now = DateTime.UtcNow;
        var end = request.EndUtc?.ToUniversalTime() ?? now;
        var start = request.StartUtc?.ToUniversalTime() ?? end.AddDays(-30);
        if (start > end) throw new ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        if (end - start > TimeSpan.FromDays(366)) throw new ArgumentException("En fazla 366 günlük bir analiz dönemi seçebilirsiniz.");
        var playbooks = await _context.Playbooks.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct);
        var playbookIds = playbooks.Select(item => item.Id).ToList();
        var runs = await _context.PlaybookRuns.AsNoTracking().Where(item => playbookIds.Contains(item.PlaybookId)).OrderByDescending(item => item.StartedAt).ToListAsync(ct);
        var pendingMailCounts = await _context.PlaybookMailLogs.AsNoTracking()
            .Where(item => playbookIds.Contains(item.PlaybookId) && item.Status == PlaybookMailStatus.Pending)
            .GroupBy(item => item.PlaybookId)
            .Select(group => new { PlaybookId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PlaybookId, item => item.Count, ct);

        var workflows = new List<SecurityAgentWorkflow>();
        var allNodeTypes = new List<string>();
        foreach (var playbook in playbooks)
        {
            var graph = PlaybookJson.Deserialize<PlaybookGraph>(playbook.GraphJson) ?? new PlaybookGraph();
            var validation = await _playbookEngine.ValidateAsync(graph, ct);
            var lastRun = runs.FirstOrDefault(item => item.PlaybookId == playbook.Id);
            var nodes = graph.Nodes.Select(node => string.IsNullOrWhiteSpace(node.Label) ? node.Type : $"{node.Label} ({node.Type})").ToList();
            allNodeTypes.AddRange(graph.Nodes.Select(node => node.Type));
            workflows.Add(new SecurityAgentWorkflow(
                playbook.Id, playbook.Name, playbook.Enabled, playbook.AutoSend, CronSchedule.Describe(playbook.ScheduleCron), nodes,
                validation.Errors, lastRun?.Status, lastRun?.StartedAt, pendingMailCounts.GetValueOrDefault(playbook.Id), lastRun?.MailsFailed ?? 0,
                PlaybookJson.Deserialize<List<PlaybookNodeLog>>(lastRun?.NodeLogJson)
                    ?.Select(node => $"{node.Label}: {node.Status} ({node.ItemsIn}->{node.ItemsOut}, {node.DurationMs} ms){(string.IsNullOrWhiteSpace(node.Message) ? string.Empty : $" - {node.Message}")}")
                    .ToList() ?? []));
        }

        var incidents = _context.Incidents.AsNoTracking().Where(item => item.Timestamp >= start && item.Timestamp <= end);
        var totalIncidents = await incidents.CountAsync(ct);
        var uniqueUsers = await incidents.Where(item => !string.IsNullOrWhiteSpace(item.UserEmail)).Select(item => item.UserEmail).Distinct().CountAsync(ct);

        return new SecurityAgentContext(
            now, start, end, playbooks.Count, playbooks.Count(item => item.Enabled), totalIncidents, uniqueUsers, workflows,
            await CountByAsync(incidents.Select(item => item.Channel), ct),
            await CountByAsync(incidents.Select(item => item.Action), ct),
            await CountByAsync(incidents.Select(item => item.Policy ?? item.RuleName), ct),
            allNodeTypes.GroupBy(item => item).OrderByDescending(group => group.Count()).Select(group => new LocalLlmCount(group.Key, group.Count())).ToList());
    }

    public async Task<SecurityAgentChatResult> ChatAsync(SecurityAgentChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) throw new ArgumentException("Mesaj zorunludur.");
        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled) throw new InvalidOperationException("Yerel LLM Laboratuvarı ayarlardan etkinleştirilmelidir.");

        var context = await GetContextAsync(new SecurityAgentContextRequest(request.StartUtc, request.EndUtc), ct);
        var reply = await GenerateAsync(settings, BuildPrompt(request, context), ct);
        return new SecurityAgentChatResult(reply, context);
    }

    public async Task<SecurityAgentWorkflowDraftResult> CreateWorkflowDraftAsync(SecurityAgentWorkflowDraftRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Goal)) throw new ArgumentException("Workflow taslağı için hedef zorunludur.");
        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled) throw new InvalidOperationException("Yerel LLM Laboratuvarı ayarlardan etkinleştirilmelidir.");

        var context = await GetContextAsync(new SecurityAgentContextRequest(request.StartUtc, request.EndUtc), ct);
        var summary = await GenerateAsync(settings, $"{BuildPrompt(new SecurityAgentChatRequest(request.Goal), context)}\nBu hedef için önerilen workflow taslağının amacını, kontrol etmek istediği anomaliyi ve kullanıcının editörde yapılandırması gereken filtre/eşik alanlarını en fazla 8 maddede açıkla.", ct);
        var nodes = new List<PlaybookNode>
        {
            new() { Id = "agent-1", Type = PlaybookNodeType.TriggerManual, Label = "Manuel Tetikleyici", X = 100, Y = 180 },
            new() { Id = "agent-2", Type = PlaybookNodeType.SourceIncidentUsers, Label = "Olay Kaydı Kullanıcıları", X = 360, Y = 180 },
            new() { Id = "agent-3", Type = PlaybookNodeType.TransformFilter, Label = "Agent Önerisi Filtresi", X = 620, Y = 180 },
            new() { Id = "agent-4", Type = PlaybookNodeType.OutputReport, Label = "Agent Taslak Çıktısı", X = 880, Y = 180 }
        };
        var graph = new PlaybookGraph
        {
            Nodes = nodes,
            Edges = nodes.Skip(1).Select((node, index) => new PlaybookEdge { Id = $"agent-edge-{index + 1}", Source = nodes[index].Id, Target = node.Id }).ToList()
        };
        var validation = await _playbookEngine.ValidateAsync(graph, ct);
        var now = DateTime.UtcNow;
        var name = $"Agent Taslağı - {request.Goal.Trim()}";
        if (name.Length > 200) name = name[..200];
        var playbook = new Playbook
        {
            Name = name,
            Description = summary.Length > 1000 ? summary[..1000] : summary,
            GraphJson = PlaybookJson.Serialize(graph),
            Enabled = false,
            AutoSend = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Playbooks.Add(playbook);
        await _context.SaveChangesAsync(ct);
        var warnings = validation.Errors.Concat(validation.Warnings).ToList();
        warnings.Add("Taslak pasif kaydedildi. Filtre ve eşik alanlarını editörde doldurun; sonra Test Et ile sonucu doğrulayın.");
        return new SecurityAgentWorkflowDraftResult(playbook.Id, playbook.Name, summary, warnings);
    }

    private async Task<LocalLlmLabSettings> GetSettingsAsync(CancellationToken ct)
    {
        var values = await _context.SystemSettings.AsNoTracking().Where(item => item.Key.StartsWith("local_llm_lab_")).ToDictionaryAsync(item => item.Key, item => item.Value, ct);
        return new LocalLlmLabSettings(
            bool.TryParse(values.GetValueOrDefault(EnabledKey), out var enabled) && enabled,
            values.GetValueOrDefault(GenerateUrlKey, "http://127.0.0.1:11434/api/generate"),
            values.GetValueOrDefault(ModelKey, "qwen3:8b"),
            double.TryParse(values.GetValueOrDefault(TemperatureKey), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temperature) ? temperature : 0.2,
            int.TryParse(values.GetValueOrDefault(MaxTokensKey), out var maxTokens) ? maxTokens : 1800);
    }

    private static async Task<IReadOnlyList<LocalLlmCount>> CountByAsync(IQueryable<string?> source, CancellationToken ct)
    {
        var rows = await source.Where(value => !string.IsNullOrWhiteSpace(value)).GroupBy(value => value!)
            .Select(group => new { Name = group.Key, Count = group.Count() }).OrderByDescending(item => item.Count).Take(12).ToListAsync(ct);
        return rows.Select(item => new LocalLlmCount(item.Name, item.Count)).ToList();
    }

    private async Task<string> GenerateAsync(LocalLlmLabSettings settings, string prompt, CancellationToken ct)
    {
        var payload = new { model = settings.Model, prompt, stream = false, options = new { temperature = settings.Temperature, num_predict = Math.Clamp(settings.MaxTokens, 256, 4096) } };
        using var response = await _httpClient.PostAsJsonAsync(settings.GenerateUrl, payload, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Yerel model isteği başarısız oldu ({(int)response.StatusCode}): {raw[..Math.Min(raw.Length, 500)]}");
        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("response", out var content)) throw new InvalidOperationException("Yerel model yanıtı 'response' alanını içermiyor.");
        return string.IsNullOrWhiteSpace(content.GetString()) ? throw new InvalidOperationException("Yerel model boş yanıt verdi.") : content.GetString()!.Trim();
    }

    private static string BuildPrompt(SecurityAgentChatRequest request, SecurityAgentContext context)
    {
        var history = string.Join("\n", (request.History ?? []).TakeLast(8).Select(item => $"{(item.Role == "assistant" ? "Agent" : "Kullanıcı")}: {item.Content}"));
        var workflows = string.Join("\n\n", context.Workflows.Select(workflow =>
            $"- #{workflow.Id} {workflow.Name}: etkin={workflow.Enabled}, otomatik_gönderim={workflow.AutoSend}, zamanlama={workflow.Schedule ?? "yok"}, son_çalışma={workflow.LastRunStatus ?? "yok"}, bekleyen_mail={workflow.PendingMails}, başarısız_mail={workflow.FailedMails}\n  Node'lar: {string.Join(" | ", workflow.Nodes.DefaultIfEmpty("yok"))}\n  Doğrulama hataları: {string.Join(" | ", workflow.ValidationErrors.DefaultIfEmpty("yok"))}\n  Son node sonuçları: {string.Join(" | ", workflow.LastRunNodeSummary.DefaultIfEmpty("veri yok"))}"));
        return $"""
Sen RADAR için salt-okunur Veri Güvenliği Kapsama Agentısın.
Görevin mevcut workflow, node, son çalıştırma ve olay dağılımına dayanarak gözden kaçabilecek anomali, veri sızıntısı deseni veya operasyonel boşlukları bulmaktır.

Kesin kurallar:
- Yalnızca aşağıdaki bağlamın kanıtladığı bilgileri kullan. Bilinmeyen noktaları açıkça 'veri yok' diye belirt.
- Bir kanal veya politikanın workflow tarafından kapsanmadığını söylemeden önce node'ları incele; genel incident kullanıcı veya metrik node'ları dolaylı kapsama sağlayabilir.
- Önerileri önceliklendir: risk gerekçesi, eksik sinyal, önerilen node/filtre/eşik ve doğrulama yöntemi yaz.
- Mail gönderme, workflow değiştirme, kod yürütme veya veritabanına erişme yetkin yoktur. Yalnızca taslak önerirsin.
- Türkçe, anlaşılır ve gerektiğinde Markdown tablo kullan.

RADAR bağlamı ({context.StartUtc:yyyy-MM-dd HH:mm} - {context.EndUtc:yyyy-MM-dd HH:mm} UTC):
Toplam olay: {context.IncidentCount}; farklı kullanıcı: {context.UniqueUsers}
Kanal dağılımı: {FormatCounts(context.Channels)}
Aksiyon dağılımı: {FormatCounts(context.Actions)}
Politika dağılımı: {FormatCounts(context.Policies)}
Kullanılan node türleri: {FormatCounts(context.NodeTypes)}

Workflow'lar:
{workflows}

Sohbet geçmişi:
{history}

Kullanıcının sorusu:
{request.Message.Trim()}
""";
    }

    private static string FormatCounts(IReadOnlyList<LocalLlmCount> counts) => counts.Count == 0 ? "veri yok" : string.Join(", ", counts.Select(item => $"{item.Name}: {item.Count}"));
}
