using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DLP.RiskAnalyzer.McpServer.Tools;

[McpServerToolType]
public class RiskTools(DlpApiClient api)
{
    [McpServerTool]
    [Description("DLP olaylarının eylem özetini getirir: BLOCK, QUARANTINE, AUTHORIZED sayıları ve toplamlar. Opsiyonel tarih aralığı filtrelenebilir.")]
    public async Task<string> GetActionSummary(
        [Description("Başlangıç tarihi (YYYY-MM-DD). Belirtilmezse tüm zamanlar.")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD). Belirtilmezse bugün.")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/action-summary", new()
        {
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Belirtilen tarihteki yüksek riskli kullanıcıları listeler (risk skoru >= 50).")]
    public async Task<string> GetHighRiskUsers(
        [Description("Tarih (YYYY-MM-DD). Belirtilmezse bugün.")] string? date = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/high-risk-users", new()
        {
            ["date"] = date
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Departman bazlı risk özetini getirir: incident sayısı, yüksek riskli kullanıcılar, ortalama skor.")]
    public async Task<string> GetDepartmentSummary(
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/department-summary", new()
        {
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Günlük risk özetini getirir (varsayılan: son 7 gün). Her gün için incident sayısı, yüksek riskli sayısı, ortalama skor.")]
    public async Task<string> GetDailySummary(
        [Description("Son kaç günlük veri (varsayılan: 7).")] int? days = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/daily-summary", new()
        {
            ["days"] = days?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Risk heatmap verisini getirir. Boyut: department, user, channel, severity vb.")]
    public async Task<string> GetRiskHeatmap(
        [Description("Boyut: department | user | channel | severity (varsayılan: department).")] string? dimension = null,
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/heatmap", new()
        {
            ["dimension"] = dimension,
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Kanal bazlı aktivite verisini getirir: Email, Cloud, USB, Print vb.")]
    public async Task<string> GetChannelActivity(
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/channel-activity", new()
        {
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Gösterge davranış (IOB) tespitlerini listeler.")]
    public async Task<string> GetIobDetections(
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/iob-detections", new()
        {
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Günlük en çok risk oluşturan kullanıcıları listeler.")]
    public async Task<string> GetTopRiskyUsersDaily(
        [Description("Tarih (YYYY-MM-DD). Belirtilmezse bugün.")] string? date = null,
        [Description("Maksimum kullanıcı sayısı (varsayılan: 10).")] int? topN = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/top-users-daily", new()
        {
            ["date"] = date,
            ["topN"] = topN?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Günlük en çok tetiklenen DLP kurallarını listeler.")]
    public async Task<string> GetTopRulesDaily(
        [Description("Tarih (YYYY-MM-DD). Belirtilmezse bugün.")] string? date = null,
        [Description("Maksimum kural sayısı (varsayılan: 10).")] int? topN = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/top-rules-daily", new()
        {
            ["date"] = date,
            ["topN"] = topN?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Risk trendi verilerini getirir: kullanıcı başına günlük/haftalık risk değişimi.")]
    public async Task<string> GetRiskTrends(
        [Description("Trend süresi (gün, varsayılan: 30).")] int? days = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/trends", new()
        {
            ["days"] = days?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Risk skoru düşüş (decay) simülasyonu yapar.")]
    public async Task<string> GetDecaySimulation(
        [Description("Simüle edilecek gün sayısı (varsayılan: 30).")] int? days = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/decay/simulation", new()
        {
            ["days"] = days?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Sayfalanmış kullanıcı listesini getirir. Arama ve sıralama desteklenir.")]
    public async Task<string> GetUserList(
        [Description("Arama terimi (e-posta veya isim).")] string? search = null,
        [Description("Sayfa numarası (varsayılan: 1).")] int? page = null,
        [Description("Sayfa boyutu (varsayılan: 20).")] int? pageSize = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/user-list", new()
        {
            ["search"] = search,
            ["page"] = page?.ToString(),
            ["pageSize"] = pageSize?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Eylem tipine göre filtrelenmiş DLP olaylarını listeler: BLOCK, QUARANTINE, AUTHORIZED, RELEASED.")]
    public async Task<string> GetIncidentsByAction(
        [Description("Eylem tipi: BLOCK | QUARANTINE | AUTHORIZED | RELEASED.")] string action,
        [Description("Sayfa numarası (varsayılan: 1).")] int? page = null,
        [Description("Sayfa boyutu (varsayılan: 20).")] int? pageSize = null,
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/incidents/by-action", new()
        {
            ["action"] = action,
            ["page"] = page?.ToString(),
            ["pageSize"] = pageSize?.ToString(),
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Anomali tespitlerini listeler.")]
    public async Task<string> GetAnomalyDetections(
        [Description("Kullanıcı e-postası (opsiyonel, filtre için).")] string? userEmail = null,
        [Description("Başlangıç tarihi (YYYY-MM-DD).")] string? startDate = null,
        [Description("Bitiş tarihi (YYYY-MM-DD).")] string? endDate = null)
    {
        var path = DlpApiClient.BuildQuery("/api/risk/anomaly/detections", new()
        {
            ["userEmail"] = userEmail,
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        return await api.GetJsonAsync(path);
    }
}
