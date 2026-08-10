using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IForcepointPolicyExceptionService
{
    Task<ForcepointExceptionToggleResult> SetExceptionEnabledAsync(int exceptionId, bool enabled, string actor, CancellationToken ct = default);
}

public record ForcepointExceptionToggleResult(
    bool Success,
    string Message,
    int ExceptionId,
    string? PolicyName,
    string? RuleName,
    string? ExceptionRuleName,
    string Enabled,
    string? ForcepointResponse = null);

public class ForcepointPolicyExceptionService : IForcepointPolicyExceptionService
{
    private const string PolicyType = "DLP";
    private readonly AnalyzerDbContext _context;
    private readonly IDlpConfigurationService _dlpConfigService;
    private readonly ILogger<ForcepointPolicyExceptionService> _logger;

    public ForcepointPolicyExceptionService(
        AnalyzerDbContext context,
        IDlpConfigurationService dlpConfigService,
        ILogger<ForcepointPolicyExceptionService> logger)
    {
        _context = context;
        _dlpConfigService = dlpConfigService;
        _logger = logger;
    }

    public async Task<ForcepointExceptionToggleResult> SetExceptionEnabledAsync(
        int exceptionId,
        bool enabled,
        string actor,
        CancellationToken ct = default)
    {
        var exception = await _context.PIExceptions
            .Include(e => e.Rule)
                .ThenInclude(r => r.Policy)
            .FirstOrDefaultAsync(e => e.Id == exceptionId, ct);

        if (exception == null)
            return Fail(exceptionId, null, null, null, enabled, "Exception kaydı bulunamadı.");

        var ruleName = exception.Rule?.RuleName;
        var exceptionName = exception.ExceptionRuleName;
        var policyName = exception.Rule?.Policy?.PolicyName;

        if (string.IsNullOrWhiteSpace(ruleName) || string.IsNullOrWhiteSpace(exceptionName))
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, "Rule veya exception adı boş olduğu için Forcepoint güncellemesi yapılamadı.");

        DlpApiSensitiveSettingsResponse config;
        try
        {
            config = await _dlpConfigService.GetSensitiveConfigAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forcepoint DLP config could not be loaded from database.");
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, "Forcepoint DLP konfigürasyonu okunamadı. Settings ekranındaki DLP API ayarlarını kontrol edin.");
        }

        using var httpClient = CreateHttpClient(config);
        var token = await GetAccessTokenAsync(httpClient, config, ct);
        if (string.IsNullOrWhiteSpace(token))
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, "Forcepoint DLP API kimlik doğrulaması başarısız.");

        var getPath = $"/dlp/rest/v1/policy/rules/exceptions?type={Uri.EscapeDataString(PolicyType)}&ruleName={Uri.EscapeDataString(ruleName)}";
        var getRequest = new HttpRequestMessage(HttpMethod.Get, getPath);
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        getRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        getRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        getRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var getResponse = await httpClient.SendAsync(getRequest, ct);
        var getBody = await getResponse.Content.ReadAsStringAsync(ct);
        if (!getResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Forcepoint exception GET failed. Rule={RuleName}, Status={Status}, Body={Body}", ruleName, getResponse.StatusCode, getBody);
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, $"Forcepoint GET başarısız: {(int)getResponse.StatusCode} {getResponse.ReasonPhrase}", getBody);
        }

        JsonNode? getPayload;
        try
        {
            getPayload = JsonNode.Parse(getBody);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Forcepoint exception GET returned invalid JSON. Rule={RuleName}", ruleName);
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, "Forcepoint GET cevabı JSON olarak okunamadı.", getBody);
        }

        if (getPayload == null)
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, "Forcepoint GET cevabı boş döndü.", getBody);

        var payload = SelectForcepointPayload(getPayload);
        var exceptionNode = FindExceptionNode(payload, exceptionName);
        if (exceptionNode == null)
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, "Forcepoint üzerinde hedef exception bulunamadı.", getBody);

        exceptionNode["enabled"] = enabled ? "true" : "false";
        EnsurePostMetadata(payload, ruleName);

        var json = payload.ToJsonString(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        var postRequest = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/policy/rules/exceptions");
        postRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        postRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var postResponse = await httpClient.SendAsync(postRequest, ct);
        var postBody = await postResponse.Content.ReadAsStringAsync(ct);

        if (!postResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Forcepoint exception POST failed. Rule={RuleName}, Exception={ExceptionName}, Status={Status}, Body={Body}",
                ruleName, exceptionName, postResponse.StatusCode, postBody);
            return Fail(exceptionId, policyName, ruleName, exceptionName, enabled, $"Forcepoint POST başarısız: {(int)postResponse.StatusCode} {postResponse.ReasonPhrase}", postBody);
        }

        exception.Enabled = enabled ? "true" : "false";
        exception.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Forcepoint exception enabled changed. Policy={PolicyName}, Rule={RuleName}, Exception={ExceptionName}, Enabled={Enabled}, Actor={Actor}",
            policyName, ruleName, exceptionName, exception.Enabled, actor);

        return new ForcepointExceptionToggleResult(
            true,
            enabled ? "Exception Forcepoint üzerinde aktif edildi." : "Exception Forcepoint üzerinde pasif edildi.",
            exceptionId,
            policyName,
            ruleName,
            exceptionName,
            exception.Enabled,
            postBody);
    }

    private static HttpClient CreateHttpClient(DlpApiSensitiveSettingsResponse config)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        var scheme = config.UseHttps ? "https" : "http";
        return new HttpClient(handler)
        {
            BaseAddress = new Uri($"{scheme}://{config.ManagerIp}:{config.ManagerPort}"),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 30)
        };
    }

    private async Task<string?> GetAccessTokenAsync(HttpClient httpClient, DlpApiSensitiveSettingsResponse config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.Password))
            return null;

        try
        {
            var headerToken = await RequestTokenWithHeadersAsync(httpClient, config, ct);
            if (!string.IsNullOrWhiteSpace(headerToken))
                return headerToken;

            return await RequestTokenWithFormAsync(httpClient, config, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forcepoint DLP API token request failed.");
            return null;
        }
    }

    private static async Task<string?> RequestTokenWithHeadersAsync(
        HttpClient httpClient,
        DlpApiSensitiveSettingsResponse config,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
        request.Headers.Add("username", config.Username);
        request.Headers.Add("password", config.Password);

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        return ExtractAccessToken(responseContent);
    }

    private static async Task<string?> RequestTokenWithFormAsync(
        HttpClient httpClient,
        DlpApiSensitiveSettingsResponse config,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", config.Username),
            new KeyValuePair<string, string>("password", config.Password)
        });

        using var response = await httpClient.PostAsync("/dlp/rest/v1/auth/access-token", content, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        return ExtractAccessToken(responseContent);
    }

    private static string? ExtractAccessToken(string responseContent)
    {
        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);
        if (tokenResponse == null) return null;

        return GetString(tokenResponse, "access_token")
            ?? GetString(tokenResponse, "accessToken")
            ?? GetString(tokenResponse, "token");
    }

    private static JsonObject? FindExceptionNode(JsonNode node, string exceptionName)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("exception_rule_name", out var nameNode)
                && string.Equals(nameNode?.GetValue<string>(), exceptionName, StringComparison.OrdinalIgnoreCase))
            {
                return obj;
            }

            foreach (var property in obj)
            {
                if (property.Value == null) continue;
                var found = FindExceptionNode(property.Value, exceptionName);
                if (found != null) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item == null) continue;
                var found = FindExceptionNode(item, exceptionName);
                if (found != null) return found;
            }
        }

        return null;
    }

    private static JsonNode SelectForcepointPayload(JsonNode root)
    {
        if (root is not JsonObject obj) return root;
        if (obj.ContainsKey("exception_rules") || obj.ContainsKey("parent_rule_name")) return obj;
        if (obj["data"] is JsonObject data) return data;
        if (obj["value"] is JsonObject value) return value;
        return obj;
    }

    private static void EnsurePostMetadata(JsonNode payload, string ruleName)
    {
        if (payload is JsonObject obj)
        {
            obj["parent_rule_name"] ??= ruleName;
            obj["policy_type"] ??= PolicyType;
        }
    }

    private static string? GetString(Dictionary<string, JsonElement> source, string key)
    {
        return source.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static ForcepointExceptionToggleResult Fail(
        int exceptionId,
        string? policyName,
        string? ruleName,
        string? exceptionName,
        bool enabled,
        string message,
        string? forcepointResponse = null) =>
        new(false, message, exceptionId, policyName, ruleName, exceptionName, enabled ? "true" : "false", forcepointResponse);
}
