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
    Task<ForcepointExceptionBulkToggleResult> SetExceptionsEnabledAsync(IEnumerable<int> exceptionIds, bool enabled, string actor, CancellationToken ct = default);
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

public record ForcepointExceptionBulkToggleItemResult(
    bool Success,
    string Message,
    int ExceptionId,
    string? PolicyName,
    string? RuleName,
    string? ExceptionRuleName,
    string Enabled,
    string? ForcepointResponse = null);

public record ForcepointExceptionBulkToggleResult(
    bool Success,
    string Message,
    int RequestedCount,
    int UpdatedCount,
    int FailedCount,
    IReadOnlyList<ForcepointExceptionBulkToggleItemResult> Items);

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
        var result = await SetExceptionsEnabledAsync(new[] { exceptionId }, enabled, actor, ct);
        var item = result.Items.FirstOrDefault(i => i.ExceptionId == exceptionId);
        if (item == null)
            return Fail(exceptionId, null, null, null, enabled, result.Message);

        return new ForcepointExceptionToggleResult(
            item.Success,
            item.Message,
            item.ExceptionId,
            item.PolicyName,
            item.RuleName,
            item.ExceptionRuleName,
            item.Enabled,
            item.ForcepointResponse);
    }

    public async Task<ForcepointExceptionBulkToggleResult> SetExceptionsEnabledAsync(
        IEnumerable<int> exceptionIds,
        bool enabled,
        string actor,
        CancellationToken ct = default)
    {
        var requestedIds = exceptionIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (requestedIds.Count == 0)
        {
            return new ForcepointExceptionBulkToggleResult(
                false,
                "Islem yapilacak exception secilmedi.",
                0,
                0,
                0,
                Array.Empty<ForcepointExceptionBulkToggleItemResult>());
        }

        var exceptions = await _context.PIExceptions
            .Include(e => e.Rule)
                .ThenInclude(r => r.Policy)
            .Where(e => requestedIds.Contains(e.Id))
            .ToListAsync(ct);

        var items = new List<ForcepointExceptionBulkToggleItemResult>();
        var exceptionsById = exceptions.ToDictionary(e => e.Id);
        foreach (var requestedId in requestedIds)
        {
            if (!exceptionsById.ContainsKey(requestedId))
            {
                items.Add(FailItem(requestedId, null, null, null, enabled, "Exception kaydi bulunamadi."));
            }
        }

        var candidates = exceptions
            .Where(e =>
            {
                var ruleName = e.Rule?.RuleName;
                var exceptionName = e.ExceptionRuleName;
                if (!string.IsNullOrWhiteSpace(ruleName) && !string.IsNullOrWhiteSpace(exceptionName))
                    return true;

                items.Add(FailItem(
                    e.Id,
                    e.Rule?.Policy?.PolicyName,
                    ruleName,
                    exceptionName,
                    enabled,
                    "Rule veya exception adi bos oldugu icin Forcepoint guncellemesi yapilamadi."));
                return false;
            })
            .ToList();

        if (candidates.Count == 0)
            return BuildBulkResult(requestedIds.Count, enabled, items);

        DlpApiSensitiveSettingsResponse config;
        try
        {
            config = await _dlpConfigService.GetSensitiveConfigAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forcepoint DLP config could not be loaded from database.");
            foreach (var candidate in candidates)
            {
                items.Add(FailItem(
                    candidate.Id,
                    candidate.Rule?.Policy?.PolicyName,
                    candidate.Rule?.RuleName,
                    candidate.ExceptionRuleName,
                    enabled,
                    "Forcepoint DLP konfigurasyonu okunamadi. Settings ekranindaki DLP API ayarlarini kontrol edin."));
            }
            return BuildBulkResult(requestedIds.Count, enabled, items);
        }

        using var httpClient = CreateHttpClient(config);
        var token = await GetAccessTokenAsync(httpClient, config, ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            foreach (var candidate in candidates)
            {
                items.Add(FailItem(
                    candidate.Id,
                    candidate.Rule?.Policy?.PolicyName,
                    candidate.Rule?.RuleName,
                    candidate.ExceptionRuleName,
                    enabled,
                    "Forcepoint DLP API kimlik dogrulamasi basarisiz."));
            }
            return BuildBulkResult(requestedIds.Count, enabled, items);
        }

        var updatedEntities = new List<PIException>();
        foreach (var group in candidates.GroupBy(e => e.Rule!.RuleName, StringComparer.OrdinalIgnoreCase))
        {
            var ruleName = group.Key!;
            var groupItems = group.ToList();
            using var getRequest = CreateGetExceptionsRequest(ruleName, token);
            var getResponse = await httpClient.SendAsync(getRequest, ct);
            var getBody = await getResponse.Content.ReadAsStringAsync(ct);
            if (!getResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Forcepoint exception GET failed. Rule={RuleName}, Status={Status}, Body={Body}", ruleName, getResponse.StatusCode, getBody);
                foreach (var candidate in groupItems)
                {
                    items.Add(FailItem(
                        candidate.Id,
                        candidate.Rule?.Policy?.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        $"Forcepoint GET basarisiz: {(int)getResponse.StatusCode} {getResponse.ReasonPhrase}",
                        getBody));
                }
                continue;
            }

            JsonNode? getPayload;
            try
            {
                getPayload = JsonNode.Parse(getBody);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Forcepoint exception GET returned invalid JSON. Rule={RuleName}", ruleName);
                foreach (var candidate in groupItems)
                {
                    items.Add(FailItem(
                        candidate.Id,
                        candidate.Rule?.Policy?.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        "Forcepoint GET cevabi JSON olarak okunamadi.",
                        getBody));
                }
                continue;
            }

            if (getPayload == null)
            {
                foreach (var candidate in groupItems)
                {
                    items.Add(FailItem(
                        candidate.Id,
                        candidate.Rule?.Policy?.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        "Forcepoint GET cevabi bos dondu.",
                        getBody));
                }
                continue;
            }

            var payload = SelectForcepointPayload(getPayload);
            var foundItems = new List<PIException>();
            foreach (var candidate in groupItems)
            {
                var exceptionNode = FindExceptionNode(payload, candidate.ExceptionRuleName!);
                if (exceptionNode == null)
                {
                    items.Add(FailItem(
                        candidate.Id,
                        candidate.Rule?.Policy?.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        "Forcepoint uzerinde hedef exception bulunamadi.",
                        getBody));
                    continue;
                }

                exceptionNode["enabled"] = enabled ? "true" : "false";
                foundItems.Add(candidate);
            }

            if (foundItems.Count == 0)
                continue;

            EnsurePostMetadata(payload, ruleName);
            var json = payload.ToJsonString(new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            using var postRequest = CreatePostExceptionsRequest(token, json);
            var postResponse = await httpClient.SendAsync(postRequest, ct);
            var postBody = await postResponse.Content.ReadAsStringAsync(ct);
            if (!postResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Forcepoint exception POST failed. Rule={RuleName}, Count={Count}, Status={Status}, Body={Body}",
                    ruleName, foundItems.Count, postResponse.StatusCode, postBody);
                foreach (var candidate in foundItems)
                {
                    items.Add(FailItem(
                        candidate.Id,
                        candidate.Rule?.Policy?.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        $"Forcepoint POST basarisiz: {(int)postResponse.StatusCode} {postResponse.ReasonPhrase}",
                        postBody));
                }
                continue;
            }

            foreach (var candidate in foundItems)
            {
                candidate.Enabled = enabled ? "true" : "false";
                candidate.UpdatedAt = DateTime.UtcNow;
                updatedEntities.Add(candidate);
                items.Add(new ForcepointExceptionBulkToggleItemResult(
                    true,
                    enabled ? "Exception Forcepoint uzerinde aktif edildi." : "Exception Forcepoint uzerinde pasif edildi.",
                    candidate.Id,
                    candidate.Rule?.Policy?.PolicyName,
                    ruleName,
                    candidate.ExceptionRuleName,
                    candidate.Enabled,
                    postBody));
            }
        }

        if (updatedEntities.Count > 0)
            await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Forcepoint bulk exception enabled changed. Requested={Requested}, Updated={Updated}, Failed={Failed}, Enabled={Enabled}, Actor={Actor}",
            requestedIds.Count, updatedEntities.Count, items.Count(i => !i.Success), enabled, actor);

        return BuildBulkResult(requestedIds.Count, enabled, items);
    }

    private static HttpRequestMessage CreateGetExceptionsRequest(string ruleName, string token)
    {
        var getPath = $"/dlp/rest/v1/policy/rules/exceptions?type={Uri.EscapeDataString(PolicyType)}&ruleName={Uri.EscapeDataString(ruleName)}";
        var request = new HttpRequestMessage(HttpMethod.Get, getPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    private static HttpRequestMessage CreatePostExceptionsRequest(string token, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/policy/rules/exceptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return request;
    }

    private static ForcepointExceptionBulkToggleResult BuildBulkResult(
        int requestedCount,
        bool enabled,
        IReadOnlyList<ForcepointExceptionBulkToggleItemResult> items)
    {
        var updatedCount = items.Count(i => i.Success);
        var failedCount = items.Count(i => !i.Success);
        var action = enabled ? "aktif edildi" : "pasif edildi";
        var message = failedCount == 0
            ? $"{updatedCount} exception Forcepoint uzerinde {action}."
            : $"{updatedCount} exception {action}, {failedCount} exception icin hata olustu.";

        return new ForcepointExceptionBulkToggleResult(
            failedCount == 0 && updatedCount == requestedCount,
            message,
            requestedCount,
            updatedCount,
            failedCount,
            items);
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

    private static ForcepointExceptionBulkToggleItemResult FailItem(
        int exceptionId,
        string? policyName,
        string? ruleName,
        string? exceptionName,
        bool enabled,
        string message,
        string? forcepointResponse = null) =>
        new(false, message, exceptionId, policyName, ruleName, exceptionName, enabled ? "true" : "false", forcepointResponse);
}
