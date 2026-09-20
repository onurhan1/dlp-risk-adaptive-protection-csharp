using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly Regex SensitiveConfigKey = new(
        "password|secret|token|credential|api[_-]?key|body|html|subject|recipient|template|(^|_)(to|cc|bcc|mail|email)($|_)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LastDaysPattern = new(
        "\\bson\\s+(\\d{1,3})\\s+g(?:ü|u)n\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RiskThresholdPattern = new(
        "\\b(?:skor|eşik|esik)\\s*(?:>=|≥|üzeri|uzeri|üstü|ustu)?\\s*(\\d{1,3})\\b|\\b(\\d{1,3})\\s*(?:ve\\s*)?(?:üzeri|uzeri|üstü|ustu)\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AnalyzerDbContext _context;
    private readonly IPlaybookEngine _playbookEngine;
    private readonly IRiskShadowService _riskShadowService;
    private readonly ILocalLlmLabService _localLlmLabService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SecurityAgentService> _logger;

    public SecurityAgentService(AnalyzerDbContext context, IPlaybookEngine playbookEngine, IRiskShadowService riskShadowService, ILocalLlmLabService localLlmLabService, HttpClient httpClient, ILogger<SecurityAgentService> logger)
    {
        _context = context;
        _playbookEngine = playbookEngine;
        _riskShadowService = riskShadowService;
        _localLlmLabService = localLlmLabService;
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
            // Legacy or hand-edited graphs may contain duplicate node IDs. Validation reports that
            // configuration error, but context loading must remain available so an admin can repair it.
            var nodeNames = graph.Nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.Id))
                .GroupBy(node => node.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group =>
                    string.IsNullOrWhiteSpace(group.First().Label) ? group.First().Type : group.First().Label,
                    StringComparer.Ordinal);
            var nodeCoverage = graph.Nodes.Select(node => new SecurityAgentNodeCoverage(
                node.Id,
                string.IsNullOrWhiteSpace(node.Label) ? node.Type : node.Label,
                node.Type,
                DescribeSafeSettings(node))).ToList();
            var connections = graph.Edges.Select(edge =>
            {
                var source = nodeNames.GetValueOrDefault(edge.Source, edge.Source);
                var target = nodeNames.GetValueOrDefault(edge.Target, edge.Target);
                return string.IsNullOrWhiteSpace(edge.SourceHandle)
                    ? $"{source} -> {target}"
                    : $"{source} [{edge.SourceHandle}] -> {target}";
            }).ToList();
            workflows.Add(new SecurityAgentWorkflow(
                playbook.Id, playbook.Name, playbook.Enabled, playbook.AutoSend, CronSchedule.Describe(playbook.ScheduleCron), nodes,
                validation.Errors, lastRun?.Status, lastRun?.StartedAt, pendingMailCounts.GetValueOrDefault(playbook.Id), lastRun?.MailsFailed ?? 0,
                PlaybookJson.Deserialize<List<PlaybookNodeLog>>(lastRun?.NodeLogJson)
                    ?.Select(node => $"{node.Label}: {node.Status} ({node.ItemsIn}->{node.ItemsOut}, {node.DurationMs} ms){(string.IsNullOrWhiteSpace(node.Message) ? string.Empty : $" - {node.Message}")}")
                    .ToList() ?? [],
                nodeCoverage,
                connections,
                graph.ReportRequestKeywords.Where(value => !string.IsNullOrWhiteSpace(value)).Take(12).ToList()));
        }

        var incidents = _context.Incidents.AsNoTracking().Where(item => item.Timestamp >= start && item.Timestamp <= end);
        var totalIncidents = await incidents.CountAsync(ct);
        var uniqueUsers = await incidents.Where(item => !string.IsNullOrWhiteSpace(item.UserEmail)).Select(item => item.UserEmail).Distinct().CountAsync(ct);

        return new SecurityAgentContext(
            now, start, end, playbooks.Count, playbooks.Count(item => item.Enabled), totalIncidents, uniqueUsers, workflows,
            await CountByAsync(incidents.Select(item => item.Channel), ct),
            await CountByAsync(incidents.Select(item => item.Action), ct),
            await CountByAsync(incidents.Select(item => item.Policy ?? item.RuleName), ct),
            allNodeTypes.GroupBy(item => item).OrderByDescending(group => group.Count()).Select(group => new LocalLlmCount(group.Key, group.Count())).ToList(),
            (await _riskShadowService.GetSnapshotAsync((int)Math.Ceiling((end - start).TotalDays), 12, ct)).Candidates);
    }

    public async Task<LocalLlmHealthCheck> GetModelHealthAsync(CancellationToken ct)
    {
        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled)
        {
            return new LocalLlmHealthCheck(false, "disabled", "Yerel LLM Laboratuvarı ayarlardan etkin değil.", null, false,
                "Yerel LLM Laboratuvarı sayfasından modeli etkinleştirin ve ayarları kaydedin.");
        }

        // Reuse the laboratory's proven, typed connectivity test so the Agent and Lab
        // report unreachable hosts, missing models, timeouts and malformed replies alike.
        return await _localLlmLabService.TestConnectionAsync(settings, ct);
    }

    public async Task<SecurityAgentChatResult> ChatAsync(SecurityAgentChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) throw new ArgumentException("Mesaj zorunludur.");
        var context = await GetContextAsync(ResolveChatContextRequest(request), ct);
        if (TryParseHighRiskListRequest(request.Message, out var minimumScore))
        {
            var days = Math.Clamp((int)Math.Ceiling((context.EndUtc - context.StartUtc).TotalDays), 7, 90);
            var result = await _riskShadowService.GetHighRiskCandidatesAsync(days, minimumScore, 20, DateOnly.FromDateTime(context.EndUtc), ct);
            return new SecurityAgentChatResult(BuildHighRiskListReply(result), context, result);
        }

        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled) throw new InvalidOperationException("Yerel LLM Laboratuvarı ayarlardan etkinleştirilmelidir.");
        var generation = await GenerateAsync(settings, BuildPrompt(request, context), ct);
        return new SecurityAgentChatResult(generation.Reply, context, null, generation.IsTruncated);
    }

    public async Task<SecurityAgentWorkflowDraftResult> CreateWorkflowDraftAsync(SecurityAgentWorkflowDraftRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Goal)) throw new ArgumentException("Workflow taslağı için hedef zorunludur.");
        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled) throw new InvalidOperationException("Yerel LLM Laboratuvarı ayarlardan etkinleştirilmelidir.");

        var context = await GetContextAsync(new SecurityAgentContextRequest(request.StartUtc, request.EndUtc), ct);
        var planResponse = await GenerateAsync(settings, BuildWorkflowPlanPrompt(request.Goal, context), ct);
        var plan = BuildWorkflowPlan(planResponse.Reply, request.Goal);
        var graph = plan.Graph;
        var validation = await _playbookEngine.ValidateAsync(graph, ct);
        var now = DateTime.UtcNow;
        var name = plan.Name;
        if (name.Length > 200) name = name[..200];
        var playbook = new Playbook
        {
            Name = name,
            Description = plan.Summary.Length > 1000 ? plan.Summary[..1000] : plan.Summary,
            GraphJson = PlaybookJson.Serialize(graph),
            Enabled = false,
            AutoSend = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Playbooks.Add(playbook);
        await _context.SaveChangesAsync(ct);
        var warnings = plan.Warnings.Concat(validation.Errors).Concat(validation.Warnings).ToList();
        warnings.Add("Taslak pasif kaydedildi. Çalıştırmadan önce node ayarlarını kontrol edin ve Test Et ile sonucu doğrulayın.");
        return new SecurityAgentWorkflowDraftResult(playbook.Id, playbook.Name, plan.Summary, warnings);
    }

    public async Task<SecurityAgentWorkflowSimulationResult> SimulateWorkflowAsync(int playbookId, CancellationToken ct)
    {
        if (playbookId <= 0) throw new ArgumentException("Geçerli bir workflow seçin.", nameof(playbookId));

        // The engine persists a run/audit record, but forceDryRun guarantees that no mail is sent.
        var run = await _playbookEngine.RunAsync(playbookId, PlaybookTriggerType.Manual, forceDryRun: true, ct: ct);
        var nodes = PlaybookJson.Deserialize<List<PlaybookNodeLog>>(run.NodeLogJson)
            ?.Select(node => $"{node.Label}: {node.Status} ({node.ItemsIn}->{node.ItemsOut})")
            .ToList() ?? [];
        return new SecurityAgentWorkflowSimulationResult(
            playbookId, run.Id, run.Status, run.DryRun, run.MailsPending, run.MailsFailed, run.MailsSkipped,
            run.ErrorMessage, nodes);
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

    private static IReadOnlyList<string> DescribeSafeSettings(PlaybookNode node)
    {
        return node.Config
            .Where(item => !SensitiveConfigKey.IsMatch(item.Key))
            .Select(item => $"{item.Key}={DescribeJsonValue(item.Value)}")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(12)
            .ToList();
    }

    private static string DescribeJsonValue(JsonElement value)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Take(8).Select(DescribeJsonValue)),
            JsonValueKind.Object => "[nesne]",
            _ => string.Empty
        };

        return text.Length > 180 ? $"{text[..177]}..." : text;
    }

    private static SecurityAgentContextRequest ResolveChatContextRequest(SecurityAgentChatRequest request)
    {
        var message = request.Message;
        var now = DateTime.UtcNow;
        var normalized = message.ToLowerInvariant();

        if (normalized.Contains("son bir hafta") || normalized.Contains("son 1 hafta") ||
            normalized.Contains("son 7 gün") || normalized.Contains("son 7 gun") ||
            normalized.Contains("geçen hafta") || normalized.Contains("gecen hafta"))
        {
            return new SecurityAgentContextRequest(now.AddDays(-7), now);
        }

        if (normalized.Contains("son bir ay") || normalized.Contains("son 1 ay") || normalized.Contains("son 30 gün") || normalized.Contains("son 30 gun"))
        {
            return new SecurityAgentContextRequest(now.AddDays(-30), now);
        }

        var match = LastDaysPattern.Match(message);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var days))
        {
            return new SecurityAgentContextRequest(now.AddDays(-Math.Clamp(days, 1, 366)), now);
        }

        return new SecurityAgentContextRequest(request.StartUtc, request.EndUtc);
    }

    private static bool TryParseHighRiskListRequest(string message, out double minimumScore)
    {
        var normalized = message.ToLowerInvariant();
        var asksForList = normalized.Contains("liste") || normalized.Contains("list") || normalized.Contains("göster") || normalized.Contains("goster") || normalized.Contains("kimler") || normalized.Contains("kullanıcı") || normalized.Contains("kullanici");
        var asksForHighRisk = normalized.Contains("yüksek risk") || normalized.Contains("yuksek risk") || normalized.Contains("high risk");
        minimumScore = 70;
        var thresholdMatch = RiskThresholdPattern.Match(message);
        if (thresholdMatch.Success)
        {
            var value = thresholdMatch.Groups[1].Success ? thresholdMatch.Groups[1].Value : thresholdMatch.Groups[2].Value;
            if (double.TryParse(value, out var parsed)) minimumScore = Math.Clamp(parsed, 0, 100);
        }
        return asksForList && asksForHighRisk;
    }

    private static string BuildHighRiskListReply(RiskShadowListResult result)
    {
        var period = $"{result.StartDate:yyyy-MM-dd} - {result.EndDate:yyyy-MM-dd} UTC";
        if (result.MatchingCandidateCount == 0)
            return $"## Yüksek riskli kullanıcılar\n\nDönem: **{period}**  \nEşik: **{result.MinimumScore:F0}+**\n\nBu eşikte aday bulunamadı. Bu sonuç, günlük risk, kişisel baz çizgisi farkı ve Isolation Forest sinyalinin deterministik birleşimine dayanır.";

        var rows = string.Join("\n", result.Candidates.Select((candidate, index) =>
            $"| {index + 1} | {candidate.UserEmail} | {candidate.ShadowScore:F1} | {candidate.Confidence} | {candidate.DailyRiskScore:F1} | {candidate.BaselineDelta:+0.0;-0.0;0.0} | {candidate.IsolationForestScore:F1} | {candidate.IncidentCount} |"));
        var visibleText = result.MatchingCandidateCount > result.Candidates.Count
            ? $"İlk **{result.Candidates.Count}** aday gösteriliyor; toplam **{result.MatchingCandidateCount}** kullanıcı eşik üzerindedir."
            : $"Toplam **{result.MatchingCandidateCount}** kullanıcı eşik üzerindedir.";
        return $"""
## Yüksek riskli kullanıcılar

Dönem: **{period}**<br />
Eşik: **{result.MinimumScore:F0}+**<br />
{visibleText}

| # | Kullanıcı | Shadow skor | Güven | Günlük risk | Baz farkı | IF skor | Olay |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: |
{rows}

Bu liste sunucuda hesaplanmıştır; model yorumu değildir. Skor; günlük risk, kullanıcının kendi baz çizgisinden sapması ve Isolation Forest sinyalini birleştirir. İnceleme öncesinde ilgili olay kanıtlarını doğrulayın.
""";
    }

    private static string BuildWorkflowPlanPrompt(string goal, SecurityAgentContext context) => $$"""
{{BuildPrompt(new SecurityAgentChatRequest(goal), context)}}

Şimdi açıklama yerine editöre kaydedilecek gerçek bir workflow grafiği tasarla.
Yalnızca aşağıdaki node türlerini kullan: {{string.Join(", ", PlaybookNodeType.All)}}.
Yalnızca JSON döndür; Markdown, kod bloğu veya JSON dışı metin kullanma.

JSON şeması:
{
  "name": "Kısa workflow adı",
  "summary": "Taslağın hangi riski yakaladığı ve neden seçildiği",
  "nodes": [
    { "type": "trigger.manual", "label": "Manuel Tetikleyici", "config": {} },
    { "type": "source.incidentUsers", "label": "Riskli kullanıcılar", "config": { "days": 7, "min_matches": 300, "channels": ["EMAIL"] } }
  ],
  "edges": [{ "source": 0, "target": 1 }, { "source": 1, "target": 2, "source_handle": "true" }]
}

Kurallar:
- Tam olarak bir tetikleyici kullan. Zamanlama gerekmedikçe trigger.manual seç.
- Kullanıcı/olay analizi için uygun bir source.* node seç; boş veya genel bir iskelet üretme.
- Gerekliyse transform.filter, logic.condition veya logic.metricThreshold ile somut eşik ekle.
- Kullanıcıya mail gönderen action.sendMail node'u ekleme. Taslak pasif kalacak; mail/rapor adımını kullanıcı editörde ekler.
- Her akışı output.report veya output.managerEscalationReport ile bitir.
- source.highRiskUsers için days, top_limit, min_risk_score; source.topActionUsers için days, top_limit, action_kind; source.highMaxMatchTransfers için days, top_limit, min_matches ayarlarını ver.
- source.incidentUsers veya source.incidentMetric kullanırsan days, top_limit/metric ve ilgili filtreleri somutlaştır.
- logic.condition için field (triggerCount veya maxMatches), op (gt/gte/lt/lte/eq), value; logic.metricThreshold için op ve value ver.
- Eşik veya filtreyi bağlamdaki olay dağılımına dayandır; kanıt yoksa muhafazakâr varsayım olduğunu summary içinde belirt.
""";

    private static AgentWorkflowPlanBuild BuildWorkflowPlan(string rawPlan, string goal)
    {
        var warnings = new List<string>();
        try
        {
            var json = ExtractJsonObject(rawPlan);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Kök değer nesne değil.");

            var graph = new PlaybookGraph();
            var indexToNodeId = new Dictionary<int, string>();
            if (!root.TryGetProperty("nodes", out var nodeList) || nodeList.ValueKind != JsonValueKind.Array)
                throw new JsonException("nodes dizisi bulunamadı.");

            var sourceIndex = 0;
            foreach (var item in nodeList.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) { sourceIndex++; continue; }
                var type = GetJsonString(item, "type");
                if (string.IsNullOrWhiteSpace(type) || !PlaybookNodeType.All.Contains(type))
                {
                    warnings.Add($"Modelin önerdiği bilinmeyen node tipi atlandı: {type ?? "boş"}.");
                    sourceIndex++;
                    continue;
                }

                var config = item.TryGetProperty("config", out var configElement) && configElement.ValueKind == JsonValueKind.Object
                    ? SanitizeAgentConfig(configElement)
                    : new Dictionary<string, JsonElement>();
                ApplyNodeDefaults(type, config);
                var node = new PlaybookNode
                {
                    Id = $"agent-{graph.Nodes.Count + 1}",
                    Type = type,
                    Label = TrimText(GetJsonString(item, "label") ?? type, 100),
                    Config = config,
                    X = 100 + (graph.Nodes.Count % 4) * 270,
                    Y = 160 + (graph.Nodes.Count / 4) * 190
                };
                indexToNodeId[sourceIndex] = node.Id;
                graph.Nodes.Add(node);
                sourceIndex++;
                if (graph.Nodes.Count >= 9) { warnings.Add("Model planındaki node sayısı 9 ile sınırlandı."); break; }
            }

            if (graph.Nodes.Count == 0) throw new JsonException("Geçerli node bulunamadı.");
            if (graph.Nodes.Count(node => PlaybookNodeType.IsTrigger(node.Type)) != 1)
                throw new JsonException("Plan tam olarak bir tetikleyici içermiyor.");

            if (root.TryGetProperty("edges", out var edgeList) && edgeList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in edgeList.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var source = GetJsonInt(item, "source") ?? GetJsonInt(item, "from");
                    var target = GetJsonInt(item, "target") ?? GetJsonInt(item, "to");
                    if (source is null || target is null || !indexToNodeId.TryGetValue(source.Value, out var sourceId) || !indexToNodeId.TryGetValue(target.Value, out var targetId) || sourceId == targetId)
                    {
                        warnings.Add("Model planındaki geçersiz bağlantı atlandı.");
                        continue;
                    }
                    graph.Edges.Add(new PlaybookEdge { Id = $"agent-edge-{graph.Edges.Count + 1}", Source = sourceId, Target = targetId, SourceHandle = GetJsonString(item, "source_handle") });
                }
            }

            if (graph.Edges.Count == 0)
            {
                for (var index = 1; index < graph.Nodes.Count; index++)
                    graph.Edges.Add(new PlaybookEdge { Id = $"agent-edge-{index}", Source = graph.Nodes[index - 1].Id, Target = graph.Nodes[index].Id });
                warnings.Add("Model bağlantı üretmediği için node'lar sıralı bağlandı.");
            }

            if (!graph.Nodes.Any(node => node.Type is PlaybookNodeType.OutputReport or PlaybookNodeType.OutputManagerEscalationReport))
            {
                var output = new PlaybookNode { Id = $"agent-{graph.Nodes.Count + 1}", Type = PlaybookNodeType.OutputReport, Label = "Agent Öneri Çıktısı", X = 100 + (graph.Nodes.Count % 4) * 270, Y = 160 + (graph.Nodes.Count / 4) * 190, Config = new Dictionary<string, JsonElement>() };
                ApplyNodeDefaults(output.Type, output.Config);
                graph.Nodes.Add(output);
                var previous = graph.Nodes[^2];
                graph.Edges.Add(new PlaybookEdge { Id = $"agent-edge-{graph.Edges.Count + 1}", Source = previous.Id, Target = output.Id });
                warnings.Add("Planın çıktısı olmadığı için rapor çıktısı eklendi.");
            }

            var name = TrimText(GetJsonString(root, "name") ?? $"Agent Taslağı - {goal}", 200);
            var summary = TrimText(GetJsonString(root, "summary") ?? $"Agent tarafından önerilen workflow: {goal}", 1000);
            return new AgentWorkflowPlanBuild(graph, name, summary, warnings);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            warnings.Add("Yerel model yapılandırılmış plan üretemedi; düzenlenebilir temel taslak oluşturuldu.");
            return CreateFallbackWorkflowPlan(goal, warnings);
        }
    }

    private static AgentWorkflowPlanBuild CreateFallbackWorkflowPlan(string goal, List<string> warnings)
    {
        var nodes = new List<PlaybookNode>
        {
            new() { Id = "agent-1", Type = PlaybookNodeType.TriggerManual, Label = "Manuel Tetikleyici", X = 100, Y = 180 },
            new() { Id = "agent-2", Type = PlaybookNodeType.SourceIncidentUsers, Label = "Olay Kaydı Kullanıcıları", X = 360, Y = 180, Config = new Dictionary<string, JsonElement>() },
            new() { Id = "agent-3", Type = PlaybookNodeType.TransformFilter, Label = "Agent Önerisi Filtresi", X = 620, Y = 180 },
            new() { Id = "agent-4", Type = PlaybookNodeType.OutputReport, Label = "Agent Taslak Çıktısı", X = 880, Y = 180, Config = new Dictionary<string, JsonElement>() }
        };
        foreach (var node in nodes) ApplyNodeDefaults(node.Type, node.Config);
        return new AgentWorkflowPlanBuild(
            new PlaybookGraph { Nodes = nodes, Edges = nodes.Skip(1).Select((node, index) => new PlaybookEdge { Id = $"agent-edge-{index + 1}", Source = nodes[index].Id, Target = node.Id }).ToList() },
            TrimText($"Agent Taslağı - {goal}", 200),
            $"Agent planı ayrıştırılamadığı için temel taslak oluşturuldu: {goal}",
            warnings);
    }

    private static Dictionary<string, JsonElement> SanitizeAgentConfig(JsonElement config) => config.EnumerateObject()
        .Where(item => !SensitiveConfigKey.IsMatch(item.Name) && item.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Array)
        .Take(16)
        .ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);

    private static void ApplyNodeDefaults(string type, Dictionary<string, JsonElement> config)
    {
        void Set(string key, object value) { if (!config.ContainsKey(key)) config[key] = JsonSerializer.SerializeToElement(value); }
        switch (type)
        {
            case PlaybookNodeType.TriggerSchedule: Set("frequency", "weekly"); Set("day_of_week", 1); Set("hour", 9); Set("minute", 0); break;
            case PlaybookNodeType.SourceWeeklyFlags: Set("days", 7); Set("criteria", WeeklyFlagCriterion.All); break;
            case PlaybookNodeType.SourceIncidentMetric: Set("days", 7); Set("metric", "total_incidents"); Set("breakdown_by", "channel"); break;
            case PlaybookNodeType.SourceHighRiskUsers: Set("days", 7); Set("top_limit", 25); Set("min_risk_score", 80); break;
            case PlaybookNodeType.SourceTopActionUsers: Set("days", 7); Set("top_limit", 25); Set("action_kind", "permit"); break;
            case PlaybookNodeType.SourceHighMaxMatchTransfers: Set("days", 7); Set("top_limit", 25); Set("min_matches", 300); break;
            case PlaybookNodeType.SourceIncidentUsers: Set("days", 7); Set("top_limit", 25); break;
            case PlaybookNodeType.LogicCondition: Set("field", "triggerCount"); Set("op", "gte"); Set("value", 2); break;
            case PlaybookNodeType.LogicMetricThreshold: Set("op", "gte"); Set("value", 1); break;
            case PlaybookNodeType.ActionSendMail: Set("recipient_mode", "user"); Set("auto_template_by_destination", true); break;
            case PlaybookNodeType.OutputReport: Set("title", "Agent Öneri Raporu"); break;
            case PlaybookNodeType.OutputManagerEscalationReport: Set("title", "Agent Yönetici Eskalasyon Çıktısı"); break;
        }
    }

    private static string ExtractJsonObject(string value)
    {
        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');
        if (start < 0 || end <= start) throw new JsonException("JSON nesnesi bulunamadı.");
        return value[start..(end + 1)];
    }

    private static string? GetJsonString(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? GetJsonInt(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;
    private static string TrimText(string value, int maxLength) => value.Trim().Length > maxLength ? value.Trim()[..maxLength] : value.Trim();
    private sealed record AgentWorkflowPlanBuild(PlaybookGraph Graph, string Name, string Summary, List<string> Warnings);

    private async Task<LocalModelGeneration> GenerateAsync(LocalLlmLabSettings settings, string prompt, CancellationToken ct)
    {
        var payload = new { model = settings.Model, prompt, stream = false, options = new { temperature = settings.Temperature, num_predict = Math.Clamp(settings.MaxTokens, 256, 4096) } };
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(settings.GenerateUrl, payload, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == StatusCodes.Status404NotFound)
                    throw new LocalLlmConnectionException("model_or_endpoint_not_found", "Yerel model veya generate uç noktası bulunamadı.",
                        "Model adını `ollama list` ile doğrulayın; URL genellikle http://sunucu:11434/api/generate olur.", false, StatusCodes.Status502BadGateway);
                throw new LocalLlmConnectionException("model_rejected_request", $"Yerel model isteği başarısız oldu ({(int)response.StatusCode}).",
                    "Generate URL, model adı ve yerel model günlüklerini kontrol edin.", (int)response.StatusCode >= 500, StatusCodes.Status502BadGateway);
            }

            using var document = JsonDocument.Parse(raw);
            if (!document.RootElement.TryGetProperty("response", out var content))
                throw new LocalLlmConnectionException("invalid_response", "Yerel model yanıtı 'response' alanını içermiyor.",
                    "Generate URL'nin Ollama uyumlu /api/generate uç noktasını gösterdiğini kontrol edin.", false, StatusCodes.Status502BadGateway);
            var reply = content.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(reply))
                throw new LocalLlmConnectionException("empty_response", "Yerel model boş yanıt verdi.",
                    "Modelin Ollama uyumlu /api/generate uç noktasını kullandığını ve model loglarını kontrol edin.", true, StatusCodes.Status502BadGateway);
            var isTruncated = document.RootElement.TryGetProperty("done_reason", out var reason)
                && string.Equals(reason.GetString(), "length", StringComparison.OrdinalIgnoreCase);
            return new LocalModelGeneration(reply, isTruncated);
        }
        catch (HttpRequestException ex)
        {
            throw new LocalLlmConnectionException("unreachable", "Yerel LLM sunucusuna ulaşılamadı.",
                "Model cihazının açık olduğunu, URL'nin Analyzer makinesinden erişildiğini ve güvenlik duvarını kontrol edin.", true, StatusCodes.Status503ServiceUnavailable, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new LocalLlmConnectionException("timeout", "Yerel model zaman aşımına uğradı.",
                "Modelin yüklü olduğundan emin olun, daha küçük kapsam seçin veya model kapasitesini kontrol edin.", true, StatusCodes.Status504GatewayTimeout, ex);
        }
        catch (JsonException ex)
        {
            throw new LocalLlmConnectionException("invalid_response", "Yerel model geçerli JSON döndürmedi.",
                "Generate URL'nin Ollama uyumlu /api/generate uç noktasını gösterdiğini kontrol edin.", false, StatusCodes.Status502BadGateway, ex);
        }
    }

    private sealed record LocalModelGeneration(string Reply, bool IsTruncated);

    private static string BuildPrompt(SecurityAgentChatRequest request, SecurityAgentContext context)
    {
        var history = string.Join("\n", (request.History ?? []).TakeLast(8).Select(item => $"{(item.Role == "assistant" ? "Agent" : "Kullanıcı")}: {item.Content}"));
        var workflows = string.Join("\n\n", context.Workflows.Select(workflow =>
            $"- #{workflow.Id} {workflow.Name}: etkin={workflow.Enabled}, otomatik_gönderim={workflow.AutoSend}, zamanlama={workflow.Schedule ?? "yok"}, son_çalışma={workflow.LastRunStatus ?? "yok"}, bekleyen_mail={workflow.PendingMails}, başarısız_mail={workflow.FailedMails}\n  Node'lar: {string.Join(" | ", workflow.Nodes.DefaultIfEmpty("yok"))}\n  Node ayarları: {string.Join(" || ", workflow.NodeCoverage.Select(node => $"{node.Label} [{node.Type}] => {(node.Settings.Count == 0 ? "ayar yok" : string.Join(", ", node.Settings))}").DefaultIfEmpty("veri yok"))}\n  Bağlantılar: {string.Join(" | ", workflow.Connections.DefaultIfEmpty("bağlantı yok"))}\n  Rapor talep anahtarları: {string.Join(" | ", workflow.ReportRequestKeywords.DefaultIfEmpty("tanımlı değil"))}\n  Doğrulama hataları: {string.Join(" | ", workflow.ValidationErrors.DefaultIfEmpty("yok"))}\n  Son node sonuçları: {string.Join(" | ", workflow.LastRunNodeSummary.DefaultIfEmpty("veri yok"))}"));
        return $"""
Sen RADAR için salt-okunur Veri Güvenliği Kapsama Agentısın.
Görevin mevcut workflow, node, son çalıştırma ve olay dağılımına dayanarak gözden kaçabilecek anomali, veri sızıntısı deseni veya operasyonel boşlukları bulmaktır.

Kesin kurallar:
- Yalnızca aşağıdaki bağlamın kanıtladığı bilgileri kullan. Bilinmeyen noktaları açıkça 'veri yok' diye belirt.
- Bir kanal veya politikanın workflow tarafından kapsanmadığını söylemeden önce node'ları incele; genel incident kullanıcı veya metrik node'ları dolaylı kapsama sağlayabilir.
- Node ayarlarındaki filtre, aksiyon, kanal, politika, hedef, zaman penceresi ve eşik bilgilerini; ayrıca bağlantı zincirini birlikte değerlendir. Eksik bağlantı, çalışmayan koşul dalı veya gereğinden geniş/dar eşik görürsen somut olarak belirt.
- Önerileri önceliklendir: risk gerekçesi, eksik sinyal, önerilen node/filtre/eşik ve doğrulama yöntemi yaz.
- Mail gönderme, workflow değiştirme, kod yürütme veya veritabanına erişme yetkin yoktur. Yalnızca taslak önerirsin.
- Türkçe, anlaşılır ve gerektiğinde Markdown tablo kullan.

RADAR bağlamı ({context.StartUtc:yyyy-MM-dd HH:mm} - {context.EndUtc:yyyy-MM-dd HH:mm} UTC):
Toplam olay: {context.IncidentCount}; farklı kullanıcı: {context.UniqueUsers}
Kanal dağılımı: {FormatCounts(context.Channels)}
Aksiyon dağılımı: {FormatCounts(context.Actions)}
Politika dağılımı: {FormatCounts(context.Policies)}
Kullanılan node türleri: {FormatCounts(context.NodeTypes)}
Shadow risk adayları: {string.Join(" | ", context.ShadowRiskCandidates.Select(x => $"{x.UserEmail}: {x.ShadowScore:F1}, güven={x.Confidence}").DefaultIfEmpty("veri yok"))}

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
