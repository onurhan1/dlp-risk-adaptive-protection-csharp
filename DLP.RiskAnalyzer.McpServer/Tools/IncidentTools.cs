using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DLP.RiskAnalyzer.McpServer.Tools;

[McpServerToolType]
public class IncidentTools(DlpApiClient api)
{
    [McpServerTool]
    [Description("DLP olaylarını listeler. Sayfalama, eylem ve tarih filtreleri desteklenir.")]
    public async Task<string> GetIncidents(
        [Description("Sayfa numarası (varsayılan: 1).")] int? page = null,
        [Description("Sayfa boyutu (varsayılan: 20, maks: 100).")] int? pageSize = null,
        [Description("Eylem filtresi: BLOCK | QUARANTINE | AUTHORIZED | RELEASED.")] string? action = null,
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/incidents", new()
        {
            ["page"] = page?.ToString(),
            ["pageSize"] = pageSize?.ToString(),
            ["action"] = action,
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Belirli bir olayın detaylarını getirir.")]
    public async Task<string> GetIncidentDetail(
        [Description("Olay ID'si.")] string incidentId)
    {
        return await api.GetJsonAsync($"/api/incidents/{Uri.EscapeDataString(incidentId)}");
    }

    [McpServerTool]
    [Description("İstisna olay istatistiklerini getirir.")]
    public async Task<string> GetExceptionIncidentStats()
    {
        return await api.GetJsonAsync("/api/incidents/exception-stats");
    }

    [McpServerTool]
    [Description("Serbest bırakılmış (released) olayları listeler. Karantinadan çıkarılan dosyalar.")]
    public async Task<string> GetReleasedIncidents(
        [Description("Sayfa numarası (varsayılan: 1).")] int? page = null,
        [Description("Sayfa boyutu (varsayılan: 20).")] int? pageSize = null)
    {
        var path = DlpApiClient.BuildQuery("/api/released-incidents", new()
        {
            ["page"] = page?.ToString(),
            ["pageSize"] = pageSize?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Serbest bırakılan olayların özetini getirir.")]
    public async Task<string> GetReleasedIncidentsSummary()
    {
        return await api.GetJsonAsync("/api/released-incidents/summary");
    }

    [McpServerTool]
    [Description("Mercek gelişmiş olay analizini listeler.")]
    public async Task<string> GetMercekIncidents(
        [Description("Sayfa numarası (varsayılan: 1).")] int? page = null,
        [Description("Sayfa boyutu (varsayılan: 20).")] int? pageSize = null)
    {
        var path = DlpApiClient.BuildQuery("/api/mercek", new()
        {
            ["page"] = page?.ToString(),
            ["pageSize"] = pageSize?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Mercek olayının detayını getirir.")]
    public async Task<string> GetMercekIncidentDetail(
        [Description("Mercek olay ID'si.")] string incidentId)
    {
        return await api.GetJsonAsync($"/api/mercek/{Uri.EscapeDataString(incidentId)}");
    }

    [McpServerTool]
    [Description("Mercek istatistiklerini getirir.")]
    public async Task<string> GetMercekStatistics()
    {
        return await api.GetJsonAsync("/api/mercek/statistics");
    }

    [McpServerTool]
    [Description("Bir olaya remediation (önlem) uygular: BLOCK, QUARANTINE veya PERMIT.")]
    public async Task<string> ApplyRemediation(
        [Description("Olay ID'si.")] string incidentId,
        [Description("Önlem türü: BLOCK | QUARANTINE | PERMIT.")] string action,
        [Description("Açıklama notu.")] string? notes = null)
    {
        return await api.PostJsonAsync($"/api/remediation/{Uri.EscapeDataString(incidentId)}/remediate", new
        {
            action,
            notes
        });
    }

    [McpServerTool]
    [Description("Olay filtreleme seçeneklerini getirir (mevcut eylem tipleri, kanallar, departmanlar vb.).")]
    public async Task<string> GetIncidentFilterOptions()
    {
        return await api.GetJsonAsync("/api/risk/incidents/filter-options");
    }
}
