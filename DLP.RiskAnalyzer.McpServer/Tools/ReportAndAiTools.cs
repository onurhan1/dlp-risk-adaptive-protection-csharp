using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DLP.RiskAnalyzer.McpServer.Tools;

[McpServerToolType]
public class ReportAndAiTools(DlpApiClient api)
{
    // ── Reports ───────────────────────────────────────────────────────────────

    [McpServerTool]
    [Description("Günlük özet raporunu JSON olarak getirir.")]
    public async Task<string> GetDailyReportData(
        [Description("Tarih (YYYY-MM-DD). Belirtilmezse bugün.")] string? date = null)
    {
        var path = DlpApiClient.BuildQuery("/api/reports/daily-summary", new()
        {
            ["date"] = date
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("PDF raporu oluşturur. Tip: daily | department | user_risk.")]
    public async Task<string> GenerateReport(
        [Description("Rapor tipi: daily | department | user_risk.")] string reportType,
        [Description("Rapor tarihi (YYYY-MM-DD, opsiyonel).")] string? date = null)
    {
        return await api.PostJsonAsync("/api/reports/generate", new
        {
            reportType,
            date
        });
    }

    [McpServerTool]
    [Description("Mevcut oluşturulan raporları listeler.")]
    public async Task<string> ListReports()
    {
        return await api.GetJsonAsync("/api/reports");
    }

    [McpServerTool]
    [Description("Kapsamlı günlük rapor verisini getirir (grafik verileri dahil).")]
    public async Task<string> GetDailyReportFullData(
        [Description("Tarih (YYYY-MM-DD). Belirtilmezse bugün.")] string? date = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/daily-report-data", new()
        {
            ["date"] = date
        });
        return await api.GetJsonAsync(path);
    }

    // ── AI & Behavioral Analysis ──────────────────────────────────────────────

    [McpServerTool]
    [Description("AI davranışsal analiz genel görünümünü getirir.")]
    public async Task<string> GetAiBehavioralOverview()
    {
        return await api.GetJsonAsync("/api/ai-behavioral/overview");
    }

    [McpServerTool]
    [Description("AI tarafından tespit edilen anomalileri listeler.")]
    public async Task<string> GetAiDetectedAnomalies(
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/ai-behavioral/anomalies", new()
        {
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Belirli bir kullanıcının Azure AI analizini getirir.")]
    public async Task<string> GetAzureAiAnalysisByUser(
        [Description("Kullanıcı e-posta adresi.")] string userEmail)
    {
        return await api.GetJsonAsync($"/api/azure-ai/by-user/{Uri.EscapeDataString(userEmail)}");
    }

    [McpServerTool]
    [Description("Chatbot ile sorgulama yapar. DLP verileri hakkında doğal dil sorusu sorabilirsiniz.")]
    public async Task<string> AskChatBot(
        [Description("Sormak istediğiniz soru (Türkçe veya İngilizce).")] string message)
    {
        return await api.PostJsonAsync("/api/chat-bot/chat", new { message });
    }

    // ── Policies ──────────────────────────────────────────────────────────────

    [McpServerTool]
    [Description("DLP politikalarını listeler.")]
    public async Task<string> GetPolicies()
    {
        return await api.GetJsonAsync("/api/policies");
    }

    [McpServerTool]
    [Description("Mevcut risk seviyesine göre politika önerileri getirir.")]
    public async Task<string> GetPolicyRecommendations(
        [Description("Risk seviyesi: low | medium | high | critical.")] string? riskLevel = null)
    {
        return await api.PostJsonAsync("/api/policies/recommendations", new { riskLevel });
    }

    [McpServerTool]
    [Description("Politika istisnalarını listeler.")]
    public async Task<string> GetPolicyExceptions()
    {
        return await api.GetJsonAsync("/api/policy-exceptions");
    }

    // ── System ────────────────────────────────────────────────────────────────

    [McpServerTool]
    [Description("Sistem ayarlarını getirir (eşik değerleri, bildirim zamanlamaları vb.).")]
    public async Task<string> GetSystemSettings()
    {
        return await api.GetJsonAsync("/api/settings");
    }

    [McpServerTool]
    [Description("Veritabanı bağlantı durumunu kontrol eder.")]
    public async Task<string> GetDatabaseStatus()
    {
        return await api.GetJsonAsync("/api/database/status");
    }

    [McpServerTool]
    [Description("Audit (denetim) loglarını listeler.")]
    public async Task<string> GetAuditLogs(
        [Description("Sayfa numarası (varsayılan: 1).")] int? page = null,
        [Description("Sayfa boyutu (varsayılan: 20).")] int? pageSize = null,
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/logs/audit", new()
        {
            ["page"] = page?.ToString(),
            ["pageSize"] = pageSize?.ToString(),
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }
}
