using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DLP.RiskAnalyzer.McpServer.Tools;

[McpServerToolType]
public class UserTrendTools(DlpApiClient api)
{
    [McpServerTool]
    [Description("Belirli bir kullanıcının günlük risk trendini getirir.")]
    public async Task<string> GetUserDailyRiskTrend(
        [Description("Kullanıcı e-posta adresi.")] string userEmail,
        [Description("Kaç günlük veri (varsayılan: 30).")] int? days = null)
    {
        var path = DlpApiClient.BuildQuery($"/api/risk-trend/user/{Uri.EscapeDataString(userEmail)}/daily", new()
        {
            ["days"] = days?.ToString()
        });
        return await api.GetJsonAsync(path);
    }

    [McpServerTool]
    [Description("Belirli bir kullanıcının kapsamlı risk analizini getirir (tüm metrikler).")]
    public async Task<string> GetUserComprehensiveRisk(
        [Description("Kullanıcı e-posta adresi.")] string userEmail)
    {
        return await api.GetJsonAsync($"/api/risk-trend/user/{Uri.EscapeDataString(userEmail)}/comprehensive");
    }

    [McpServerTool]
    [Description("Belirli bir kullanıcının haftalık risk trendini getirir.")]
    public async Task<string> GetUserWeeklyTrend(
        [Description("Kullanıcı e-posta adresi.")] string userEmail)
    {
        return await api.GetJsonAsync($"/api/risk-trend/user/{Uri.EscapeDataString(userEmail)}/weekly-trend");
    }

    [McpServerTool]
    [Description("Belirli bir kullanıcının aylık risk trendini getirir.")]
    public async Task<string> GetUserMonthlyTrend(
        [Description("Kullanıcı e-posta adresi.")] string userEmail)
    {
        return await api.GetJsonAsync($"/api/risk-trend/user/{Uri.EscapeDataString(userEmail)}/monthly-trend");
    }

    [McpServerTool]
    [Description("Belirli bir kullanıcının çeyreklik (3 aylık) risk trendini getirir.")]
    public async Task<string> GetUserQuarterlyTrend(
        [Description("Kullanıcı e-posta adresi.")] string userEmail)
    {
        return await api.GetJsonAsync($"/api/risk-trend/user/{Uri.EscapeDataString(userEmail)}/quarterly-trend");
    }

    [McpServerTool]
    [Description("Belirli bir kullanıcıya ait anomalileri listeler.")]
    public async Task<string> GetUserAnomalies(
        [Description("Kullanıcı e-posta adresi.")] string userEmail)
    {
        return await api.GetJsonAsync($"/api/risk-trend/user/{Uri.EscapeDataString(userEmail)}/anomalies");
    }
}
