using OpenAI.Chat;
using System.Text.Json;

namespace DLP.RiskAnalyzer.McpServer;

/// <summary>
/// Tüm DLP tool'larını merkezi olarak tanımlar.
/// Hem MCP hem de LLM function calling için tek kaynak.
/// </summary>
public class ToolRegistry
{
    private readonly DlpApiClient _api;

    // Her tool: (isim, handler) ikilisi
    private readonly Dictionary<string, Func<JsonElement, Task<string>>> _handlers;

    public ToolRegistry(DlpApiClient api)
    {
        _api = api;
        _handlers = BuildHandlers();
    }

    // ── Tanımlar (LLM'e gönderilir) ─────────────────────────────────────────

    public IReadOnlyList<ChatTool> GetChatTools() => _definitions;

    private static readonly List<ChatTool> _definitions =
    [
        Tool("get_action_summary",
            "DLP olaylarının eylem özetini getirir: BLOCK, QUARANTINE, AUTHORIZED sayıları ve toplamlar.",
            Props(
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)")
            )),

        Tool("get_high_risk_users",
            "Belirtilen tarihteki yüksek riskli kullanıcıları listeler (risk skoru >= 50).",
            Props(
                Str("date", "Tarih YYYY-MM-DD (opsiyonel, varsayılan: bugün)")
            )),

        Tool("get_department_summary",
            "Departman bazlı risk özetini getirir: incident sayısı, yüksek riskli kullanıcılar, ortalama skor.",
            Props(
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)")
            )),

        Tool("get_daily_summary",
            "Günlük risk özetini getirir. Her gün için incident sayısı, yüksek riskli sayısı, ortalama skor.",
            Props(
                Str("days", "Son kaç gün (varsayılan: 7, opsiyonel)")
            )),

        Tool("get_risk_heatmap",
            "Risk heatmap verisini getirir. Boyut: department, user, channel, severity.",
            Props(
                Str("dimension", "Boyut: department | user | channel | severity"),
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)")
            )),

        Tool("get_channel_activity",
            "Kanal bazlı DLP aktivitesini getirir: Email, Cloud, USB, Print vb.",
            Props(
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)")
            )),

        Tool("get_top_risky_users_daily",
            "Günlük en çok risk oluşturan kullanıcıları listeler.",
            Props(
                Str("date", "Tarih YYYY-MM-DD (opsiyonel, varsayılan: bugün)"),
                Str("topN", "Kaç kullanıcı (varsayılan: 10, opsiyonel)")
            )),

        Tool("get_top_rules_daily",
            "Günlük en çok tetiklenen DLP kurallarını listeler.",
            Props(
                Str("date", "Tarih YYYY-MM-DD (opsiyonel, varsayılan: bugün)"),
                Str("topN", "Kaç kural (varsayılan: 10, opsiyonel)")
            )),

        Tool("get_risk_trends",
            "Risk trendi verilerini getirir: kullanıcı başına günlük/haftalık risk değişimi.",
            Props(
                Str("days", "Kaç günlük trend (varsayılan: 30, opsiyonel)")
            )),

        Tool("get_incidents_by_action",
            "Eylem tipine göre filtrelenmiş DLP olaylarını listeler.",
            Props(
                Str("action",    "Eylem tipi: BLOCK | QUARANTINE | AUTHORIZED | RELEASED (zorunlu)"),
                Str("page",      "Sayfa numarası (varsayılan: 1, opsiyonel)"),
                Str("pageSize",  "Sayfa boyutu max 100 (varsayılan: 20, opsiyonel)"),
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)")
            )),

        Tool("get_user_daily_risk_trend",
            "Belirli bir kullanıcının günlük risk trendini getirir.",
            Props(
                Str("userEmail", "Kullanıcı e-posta adresi (zorunlu)"),
                Str("days",      "Kaç günlük veri (varsayılan: 30, opsiyonel)")
            )),

        Tool("get_user_comprehensive_risk",
            "Belirli bir kullanıcının kapsamlı risk analizini getirir (tüm metrikler).",
            Props(
                Str("userEmail", "Kullanıcı e-posta adresi (zorunlu)")
            )),

        Tool("get_anomaly_detections",
            "Anomali tespitlerini listeler.",
            Props(
                Str("userEmail", "Kullanıcı e-posta adresi (opsiyonel, filtre için)"),
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)")
            )),

        Tool("get_ai_behavioral_overview",
            "AI tabanlı davranışsal analiz genel görünümünü getirir.",
            Props()),

        Tool("get_ai_detected_anomalies",
            "AI tarafından tespit edilen anomalileri listeler.",
            Props(
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)")
            )),

        Tool("get_azure_ai_analysis_by_user",
            "Belirli bir kullanıcının Azure AI analizini getirir.",
            Props(
                Str("userEmail", "Kullanıcı e-posta adresi (zorunlu)")
            )),

        Tool("get_policies",
            "DLP politikalarını listeler.",
            Props()),

        Tool("get_policy_exceptions",
            "Politika istisnalarını listeler.",
            Props()),

        Tool("get_mercek_statistics",
            "Mercek gelişmiş olay analiz istatistiklerini getirir.",
            Props()),

        Tool("get_daily_report_data",
            "Kapsamlı günlük rapor verisini getirir (grafik verileri dahil).",
            Props(
                Str("date", "Tarih YYYY-MM-DD (opsiyonel, varsayılan: bugün)")
            )),

        Tool("get_system_settings",
            "Sistem ayarlarını getirir: risk eşik değerleri, bildirim zamanlamaları vb.",
            Props()),

        Tool("get_audit_logs",
            "Sistem denetim (audit) loglarını listeler.",
            Props(
                Str("startDate", "Başlangıç tarihi YYYY-MM-DD (opsiyonel)"),
                Str("endDate",   "Bitiş tarihi YYYY-MM-DD (opsiyonel)"),
                Str("page",      "Sayfa numarası (opsiyonel)"),
                Str("pageSize",  "Sayfa boyutu (opsiyonel)")
            )),
    ];

    // ── Handler'lar ───────────────────────────────────────────────────────────

    private Dictionary<string, Func<JsonElement, Task<string>>> BuildHandlers() => new()
    {
        ["get_action_summary"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/action-summary", new()
            {
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate")
            })),

        ["get_high_risk_users"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/high-risk-users", new()
            {
                ["date"] = Opt(args, "date")
            })),

        ["get_department_summary"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/department-summary", new()
            {
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate")
            })),

        ["get_daily_summary"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/daily-summary", new()
            {
                ["days"] = Opt(args, "days")
            })),

        ["get_risk_heatmap"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/heatmap", new()
            {
                ["dimension"] = Opt(args, "dimension"),
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate")
            })),

        ["get_channel_activity"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/channel-activity", new()
            {
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate")
            })),

        ["get_top_risky_users_daily"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/top-users-daily", new()
            {
                ["date"] = Opt(args, "date"),
                ["topN"] = Opt(args, "topN")
            })),

        ["get_top_rules_daily"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/top-rules-daily", new()
            {
                ["date"] = Opt(args, "date"),
                ["topN"] = Opt(args, "topN")
            })),

        ["get_risk_trends"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/trends", new()
            {
                ["days"] = Opt(args, "days")
            })),

        ["get_incidents_by_action"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/incidents/by-action", new()
            {
                ["action"]    = Opt(args, "action"),
                ["page"]      = Opt(args, "page"),
                ["pageSize"]  = Opt(args, "pageSize"),
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate")
            })),

        ["get_user_daily_risk_trend"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery(
                $"/api/risk-trend/user/{Uri.EscapeDataString(Req(args, "userEmail"))}/daily", new()
                {
                    ["days"] = Opt(args, "days")
                })),

        ["get_user_comprehensive_risk"] = async args =>
            await _api.GetJsonAsync(
                $"/api/risk-trend/user/{Uri.EscapeDataString(Req(args, "userEmail"))}/comprehensive"),

        ["get_anomaly_detections"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/anomaly/detections", new()
            {
                ["userEmail"] = Opt(args, "userEmail"),
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate")
            })),

        ["get_ai_behavioral_overview"] = async _ =>
            await _api.GetJsonAsync("/api/ai-behavioral/overview"),

        ["get_ai_detected_anomalies"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/ai-behavioral/anomalies", new()
            {
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate")
            })),

        ["get_azure_ai_analysis_by_user"] = async args =>
            await _api.GetJsonAsync(
                $"/api/azure-ai/by-user/{Uri.EscapeDataString(Req(args, "userEmail"))}"),

        ["get_policies"] = async _ =>
            await _api.GetJsonAsync("/api/policies"),

        ["get_policy_exceptions"] = async _ =>
            await _api.GetJsonAsync("/api/policy-exceptions"),

        ["get_mercek_statistics"] = async _ =>
            await _api.GetJsonAsync("/api/mercek/statistics"),

        ["get_daily_report_data"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/risk/daily-report-data", new()
            {
                ["date"] = Opt(args, "date")
            })),

        ["get_system_settings"] = async _ =>
            await _api.GetJsonAsync("/api/settings"),

        ["get_audit_logs"] = async args =>
            await _api.GetJsonAsync(DlpApiClient.BuildQuery("/api/logs/audit", new()
            {
                ["startDate"] = Opt(args, "startDate"),
                ["endDate"]   = Opt(args, "endDate"),
                ["page"]      = Opt(args, "page"),
                ["pageSize"]  = Opt(args, "pageSize")
            })),
    };

    public async Task<string> InvokeAsync(string toolName, JsonElement args)
    {
        if (_handlers.TryGetValue(toolName, out var handler))
            return await handler(args);

        return $"{{\"error\": \"Bilinmeyen tool: {toolName}\"}}";
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private static string? Opt(JsonElement el, string key)
    {
        if (el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(key, out var val) &&
            val.ValueKind != JsonValueKind.Null)
            return val.GetString();
        return null;
    }

    private static string Req(JsonElement el, string key)
        => Opt(el, key) ?? throw new ArgumentException($"'{key}' parametresi zorunludur.");

    private static ChatTool Tool(string name, string description, BinaryData parameters)
        => ChatTool.CreateFunctionTool(name, description, parameters);

    private static BinaryData Props(params (string name, string desc)[] props)
    {
        var properties = new Dictionary<string, object>();
        foreach (var (name, desc) in props)
            properties[name] = new { type = "string", description = desc };

        var schema = new
        {
            type = "object",
            properties,
            required = Array.Empty<string>()
        };
        return BinaryData.FromObjectAsJson(schema);
    }

    private static (string name, string desc) Str(string name, string desc) => (name, desc);
}
