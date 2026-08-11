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
    Task<ForcepointExceptionBulkToggleResult> SetExceptionReferencesEnabledAsync(IEnumerable<ForcepointExceptionToggleReference> references, bool enabled, string actor, CancellationToken ct = default);
    Task<ForcepointExceptionBulkTogglePreviewResult> PreviewExceptionReferencesEnabledAsync(IEnumerable<ForcepointExceptionToggleReference> references, bool enabled, CancellationToken ct = default);
}

public record ForcepointExceptionToggleReference(
    string? PolicyName,
    string RuleName,
    string ExceptionRuleName);

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

public record ForcepointExceptionTogglePreviewItem(
    bool Success,
    bool SafeToPost,
    string Message,
    string? PolicyName,
    string RuleName,
    IReadOnlyList<string> RequestedExceptionRuleNames,
    int OriginalExceptionCount,
    int PreviewExceptionCount,
    IReadOnlyList<string> OriginalExceptionRuleNames,
    IReadOnlyList<string> PreviewExceptionRuleNames,
    string? PayloadPreview = null,
    string? RequestedEnabled = null,
    IReadOnlyDictionary<string, string>? PreviewEnabledByException = null);

public record ForcepointExceptionBulkTogglePreviewResult(
    bool Success,
    string Message,
    int RequestedCount,
    IReadOnlyList<ForcepointExceptionTogglePreviewItem> Items);

internal record ForcepointExceptionToggleCandidate(
    int ExceptionId,
    string? PolicyName,
    string RuleName,
    string ExceptionRuleName,
    PIException? Entity);

internal record ForcepointPostAttemptResult(
    bool Success,
    Uri? Uri,
    System.Net.HttpStatusCode StatusCode,
    string? ReasonPhrase,
    string Body);

public class ForcepointPolicyExceptionService : IForcepointPolicyExceptionService
{
    private const string PolicyType = "DLP";
    private const string ForcepointExceptionWriteDisabledMessage =
        "Forcepoint exception aktif/pasif islemi devre disi. Forcepoint POST API mevcut exceptionlari silebildigi icin platformdan yazma islemi durduruldu; sadece preview ve sync kullanilabilir.";
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
            return EmptyBulkResult();

        _logger.LogWarning(
            "Forcepoint exception write blocked. Requested={Requested}, Enabled={Enabled}, Actor={Actor}",
            requestedIds.Count,
            enabled,
            actor);
        return new ForcepointExceptionBulkToggleResult(
            false,
            ForcepointExceptionWriteDisabledMessage,
            requestedIds.Count,
            0,
            requestedIds.Count,
            requestedIds.Select(id => FailItem(id, null, null, null, enabled, ForcepointExceptionWriteDisabledMessage)).ToList());

#pragma warning disable CS0162
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
                items.Add(FailItem(requestedId, null, null, null, enabled, "Exception kaydi bulunamadi."));
        }

        var candidates = exceptions
            .Select(e => CreateCandidate(e, enabled, items))
            .Where(candidate => candidate != null)
            .Select(candidate => candidate!)
            .ToList();

        return await ExecuteBulkToggleAsync(candidates, requestedIds.Count, enabled, actor, items, ct);
#pragma warning restore CS0162
    }

    public async Task<ForcepointExceptionBulkToggleResult> SetExceptionReferencesEnabledAsync(
        IEnumerable<ForcepointExceptionToggleReference> references,
        bool enabled,
        string actor,
        CancellationToken ct = default)
    {
        var requestedRefs = references
            .Where(r => !string.IsNullOrWhiteSpace(r.RuleName) && !string.IsNullOrWhiteSpace(r.ExceptionRuleName))
            .GroupBy(r => BuildRefKey(r.PolicyName, r.RuleName, r.ExceptionRuleName))
            .Select(g => g.First())
            .ToList();

        if (requestedRefs.Count == 0)
            return EmptyBulkResult();

        _logger.LogWarning(
            "Forcepoint exception reference write blocked. Requested={Requested}, Enabled={Enabled}, Actor={Actor}",
            requestedRefs.Count,
            enabled,
            actor);
        return new ForcepointExceptionBulkToggleResult(
            false,
            ForcepointExceptionWriteDisabledMessage,
            requestedRefs.Count,
            0,
            requestedRefs.Count,
            requestedRefs
                .Select(r => FailItem(0, r.PolicyName, r.RuleName, r.ExceptionRuleName, enabled, ForcepointExceptionWriteDisabledMessage))
                .ToList());

#pragma warning disable CS0162
        var ruleNames = requestedRefs.Select(r => r.RuleName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var localExceptions = await _context.PIExceptions
            .Include(e => e.Rule)
                .ThenInclude(r => r.Policy)
            .Where(e => e.Rule != null && e.Rule.RuleName != null && ruleNames.Contains(e.Rule.RuleName))
            .ToListAsync(ct);

        var localLookup = localExceptions
            .Where(e => e.Rule != null)
            .GroupBy(e => BuildRefKey(e.Rule!.Policy?.PolicyName, e.Rule.RuleName, e.ExceptionRuleName))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var candidates = requestedRefs
            .Select(r =>
            {
                localLookup.TryGetValue(BuildRefKey(r.PolicyName, r.RuleName, r.ExceptionRuleName), out var entity);
                return new ForcepointExceptionToggleCandidate(
                    entity?.Id ?? 0,
                    r.PolicyName,
                    r.RuleName,
                    r.ExceptionRuleName,
                    entity);
            })
            .ToList();

        return await ExecuteBulkToggleAsync(
            candidates,
            requestedRefs.Count,
            enabled,
            actor,
            new List<ForcepointExceptionBulkToggleItemResult>(),
            ct);
#pragma warning restore CS0162
    }

    public async Task<ForcepointExceptionBulkTogglePreviewResult> PreviewExceptionReferencesEnabledAsync(
        IEnumerable<ForcepointExceptionToggleReference> references,
        bool enabled,
        CancellationToken ct = default)
    {
        var requestedRefs = references
            .Where(r => !string.IsNullOrWhiteSpace(r.RuleName) && !string.IsNullOrWhiteSpace(r.ExceptionRuleName))
            .GroupBy(r => BuildRefKey(r.PolicyName, r.RuleName, r.ExceptionRuleName))
            .Select(g => g.First())
            .ToList();

        if (requestedRefs.Count == 0)
        {
            return new ForcepointExceptionBulkTogglePreviewResult(
                false,
                "Islem yapilacak exception secilmedi.",
                0,
                Array.Empty<ForcepointExceptionTogglePreviewItem>());
        }

        DlpApiSensitiveSettingsResponse config;
        try
        {
            config = await _dlpConfigService.GetSensitiveConfigAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forcepoint DLP config could not be loaded from database.");
            return new ForcepointExceptionBulkTogglePreviewResult(
                false,
                "Forcepoint DLP konfigurasyonu okunamadi.",
                requestedRefs.Count,
                Array.Empty<ForcepointExceptionTogglePreviewItem>());
        }

        using var httpClient = CreateHttpClient(config);
        var token = await GetAccessTokenAsync(httpClient, config, ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ForcepointExceptionBulkTogglePreviewResult(
                false,
                "Forcepoint DLP API kimlik dogrulamasi basarisiz.",
                requestedRefs.Count,
                Array.Empty<ForcepointExceptionTogglePreviewItem>());
        }

        var items = new List<ForcepointExceptionTogglePreviewItem>();
        foreach (var group in requestedRefs.GroupBy(e => BuildRefKey(e.PolicyName, e.RuleName, null), StringComparer.OrdinalIgnoreCase))
        {
            var groupItems = group.ToList();
            var ruleName = groupItems[0].RuleName;
            var policyName = groupItems[0].PolicyName;
            var requestedNames = groupItems.Select(i => i.ExceptionRuleName).ToList();

            using var getRequest = CreateGetExceptionsRequest(httpClient, ruleName, policyName, token);
            var getResponse = await httpClient.SendAsync(getRequest, ct);
            var getBody = await getResponse.Content.ReadAsStringAsync(ct);
            if (!getResponse.IsSuccessStatusCode)
            {
                items.Add(new ForcepointExceptionTogglePreviewItem(
                    false,
                    false,
                    $"Forcepoint GET basarisiz: {(int)getResponse.StatusCode} {getResponse.ReasonPhrase}",
                    policyName,
                    ruleName,
                    requestedNames,
                    0,
                    0,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Truncate(getBody, 2000)));
                continue;
            }

            JsonNode? getPayload;
            try
            {
                getPayload = JsonNode.Parse(getBody);
            }
            catch (JsonException)
            {
                items.Add(new ForcepointExceptionTogglePreviewItem(
                    false,
                    false,
                    "Forcepoint GET cevabi JSON olarak okunamadi.",
                    policyName,
                    ruleName,
                    requestedNames,
                    0,
                    0,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Truncate(getBody, 2000)));
                continue;
            }

            if (getPayload == null)
            {
                items.Add(new ForcepointExceptionTogglePreviewItem(
                    false,
                    false,
                    "Forcepoint GET cevabi bos dondu.",
                    policyName,
                    ruleName,
                    requestedNames,
                    0,
                    0,
                    Array.Empty<string>(),
                    Array.Empty<string>()));
                continue;
            }

            var payload = SelectForcepointPayload(getPayload);
            var originalNames = ExtractTopLevelExceptionRuleNames(payload);
            var missingTargets = new List<string>();
            foreach (var requestedName in requestedNames)
            {
                var exceptionNode = FindExceptionNode(payload, requestedName);
                if (exceptionNode == null)
                {
                    missingTargets.Add(requestedName);
                    continue;
                }

                exceptionNode["enabled"] = enabled ? "true" : "false";
            }

            EnsurePostMetadata(payload, policyName, ruleName);
            SanitizePayloadForPost(payload);
            var previewNames = ExtractTopLevelExceptionRuleNames(payload);
            var requestedEnabled = enabled ? "true" : "false";
            var previewEnabledByException = requestedNames.ToDictionary(
                name => name,
                name => GetExceptionEnabled(payload, name) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
            var hasSameExceptionSet = HasSameValues(originalNames, previewNames);
            var hasNonEmptyExceptionList = previewNames.Count > 0;
            var hasRequestedEnabled = missingTargets.Count == 0 &&
                                      previewEnabledByException.Values.All(value =>
                                          string.Equals(value, requestedEnabled, StringComparison.OrdinalIgnoreCase));
            var safeToPost = missingTargets.Count == 0 &&
                             hasNonEmptyExceptionList &&
                             hasSameExceptionSet &&
                             hasRequestedEnabled;
            var json = payload.ToJsonString(new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            items.Add(new ForcepointExceptionTogglePreviewItem(
                safeToPost,
                safeToPost,
                safeToPost
                    ? "Preview basarili. POST payload'i exception listesini koruyor."
                    : BuildUnsafePreviewMessage(missingTargets, hasNonEmptyExceptionList, hasSameExceptionSet, hasRequestedEnabled),
                policyName,
                ruleName,
                requestedNames,
                originalNames.Count,
                previewNames.Count,
                originalNames,
                previewNames,
                Truncate(json, 4000),
                requestedEnabled,
                previewEnabledByException));
        }

        var success = items.Count > 0 && items.All(i => i.Success && i.SafeToPost);
        return new ForcepointExceptionBulkTogglePreviewResult(
            success,
            success
                ? "Preview basarili. Herhangi bir Forcepoint POST islemi yapilmadi."
                : "Preview guvenli degil veya tamamlanamadi. Forcepoint POST islemi yapilmadi.",
            requestedRefs.Count,
            items);
    }

    private async Task<ForcepointExceptionBulkToggleResult> ExecuteBulkToggleAsync(
        IReadOnlyList<ForcepointExceptionToggleCandidate> candidates,
        int requestedCount,
        bool enabled,
        string actor,
        List<ForcepointExceptionBulkToggleItemResult> items,
        CancellationToken ct)
    {
        if (candidates.Count == 0)
            return BuildBulkResult(requestedCount, enabled, items);

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
                    candidate.ExceptionId,
                    candidate.PolicyName,
                    candidate.RuleName,
                    candidate.ExceptionRuleName,
                    enabled,
                    "Forcepoint DLP konfigurasyonu okunamadi. Settings ekranindaki DLP API ayarlarini kontrol edin."));
            }
            return BuildBulkResult(requestedCount, enabled, items);
        }

        using var httpClient = CreateHttpClient(config);
        var token = await GetAccessTokenAsync(httpClient, config, ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            foreach (var candidate in candidates)
            {
                items.Add(FailItem(
                    candidate.ExceptionId,
                    candidate.PolicyName,
                    candidate.RuleName,
                    candidate.ExceptionRuleName,
                    enabled,
                    "Forcepoint DLP API kimlik dogrulamasi basarisiz."));
            }
            return BuildBulkResult(requestedCount, enabled, items);
        }

        var updatedEntities = new List<PIException>();
        var updatedSyncedExceptions = new List<PolicyRuleException>();
        foreach (var group in candidates.GroupBy(e => BuildRefKey(e.PolicyName, e.RuleName, null), StringComparer.OrdinalIgnoreCase))
        {
            var groupItems = group.ToList();
            var ruleName = groupItems[0].RuleName;
            var policyName = groupItems[0].PolicyName;
            using var getRequest = CreateGetExceptionsRequest(httpClient, ruleName, policyName, token);
            var getResponse = await httpClient.SendAsync(getRequest, ct);
            var getBody = await getResponse.Content.ReadAsStringAsync(ct);
            if (!getResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Forcepoint exception GET failed. Rule={RuleName}, Status={Status}, Body={Body}", ruleName, getResponse.StatusCode, getBody);
                foreach (var candidate in groupItems)
                {
                    items.Add(FailItem(
                        candidate.ExceptionId,
                        candidate.PolicyName,
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
                        candidate.ExceptionId,
                        candidate.PolicyName,
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
                        candidate.ExceptionId,
                        candidate.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        "Forcepoint GET cevabi bos dondu.",
                        getBody));
                }
                continue;
            }

            var payload = SelectForcepointPayload(getPayload);
            var originalNames = ExtractTopLevelExceptionRuleNames(payload);
            var foundItems = new List<ForcepointExceptionToggleCandidate>();
            var missingTargets = new List<string>();
            foreach (var candidate in groupItems)
            {
                var exceptionNode = FindExceptionNode(payload, candidate.ExceptionRuleName);
                if (exceptionNode == null)
                {
                    missingTargets.Add(candidate.ExceptionRuleName);
                    continue;
                }

                exceptionNode["enabled"] = enabled ? "true" : "false";
                foundItems.Add(candidate);
            }

            if (missingTargets.Count > 0)
            {
                foreach (var candidate in groupItems.Where(i => missingTargets.Contains(i.ExceptionRuleName, StringComparer.OrdinalIgnoreCase)))
                {
                    items.Add(FailItem(
                        candidate.ExceptionId,
                        candidate.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        "Forcepoint uzerinde hedef exception bulunamadi.",
                        getBody));
                }
            }

            if (foundItems.Count == 0)
                continue;

            EnsurePostMetadata(payload, policyName, ruleName);
            SanitizePayloadForPost(payload);
            var previewNames = ExtractTopLevelExceptionRuleNames(payload);
            var requestedEnabled = enabled ? "true" : "false";
            var previewEnabledByException = foundItems.ToDictionary(
                item => item.ExceptionRuleName,
                item => GetExceptionEnabled(payload, item.ExceptionRuleName) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
            var hasSameExceptionSet = HasSameValues(originalNames, previewNames);
            var hasNonEmptyExceptionList = previewNames.Count > 0;
            var hasRequestedEnabled = previewEnabledByException.Values.All(value =>
                string.Equals(value, requestedEnabled, StringComparison.OrdinalIgnoreCase));
            var json = payload.ToJsonString(new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            if (!hasNonEmptyExceptionList || !hasSameExceptionSet || !hasRequestedEnabled)
            {
                var message = BuildUnsafePreviewMessage(
                    Array.Empty<string>(),
                    hasNonEmptyExceptionList,
                    hasSameExceptionSet,
                    hasRequestedEnabled);
                _logger.LogWarning(
                    "Forcepoint exception POST blocked by safety preview. Policy={PolicyName}, Rule={RuleName}, Count={Count}, Message={Message}, PayloadPreview={PayloadPreview}",
                    policyName,
                    ruleName,
                    foundItems.Count,
                    message,
                    Truncate(json, 2000));

                foreach (var candidate in foundItems)
                {
                    items.Add(FailItem(
                        candidate.ExceptionId,
                        candidate.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        message,
                        Truncate(json, 2000)));
                }
                continue;
            }

            var postResult = await SendPostExceptionsAsync(httpClient, token, json, ct);
            var postBody = postResult.Body;
            if (!postResult.Success)
            {
                _logger.LogWarning(
                    "Forcepoint exception POST failed. Policy={PolicyName}, Rule={RuleName}, Count={Count}, Uri={Uri}, Status={Status}, Body={Body}, PayloadPreview={PayloadPreview}",
                    policyName,
                    ruleName,
                    foundItems.Count,
                    postResult.Uri,
                    postResult.StatusCode,
                    postBody,
                    Truncate(json, 2000));
                foreach (var candidate in foundItems)
                {
                    items.Add(FailItem(
                        candidate.ExceptionId,
                        candidate.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        $"Forcepoint POST basarisiz: {(int)postResult.StatusCode} {postResult.ReasonPhrase}",
                        postBody));
                }
                continue;
            }

            using var verifyRequest = CreateGetExceptionsRequest(httpClient, ruleName, policyName, token);
            var verifyResponse = await httpClient.SendAsync(verifyRequest, ct);
            var verifyBody = await verifyResponse.Content.ReadAsStringAsync(ct);
            if (!verifyResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Forcepoint exception verification GET failed after POST. Policy={PolicyName}, Rule={RuleName}, Count={Count}, Status={Status}, Body={Body}",
                    policyName,
                    ruleName,
                    foundItems.Count,
                    verifyResponse.StatusCode,
                    verifyBody);

                foreach (var candidate in foundItems)
                {
                    items.Add(FailItem(
                        candidate.ExceptionId,
                        candidate.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        $"Forcepoint POST yapildi ancak dogrulama GET basarisiz: {(int)verifyResponse.StatusCode} {verifyResponse.ReasonPhrase}",
                        verifyBody));
                }
                continue;
            }

            JsonNode? verifyPayloadRoot;
            try
            {
                verifyPayloadRoot = JsonNode.Parse(verifyBody);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Forcepoint exception verification GET returned invalid JSON. Policy={PolicyName}, Rule={RuleName}", policyName, ruleName);
                foreach (var candidate in foundItems)
                {
                    items.Add(FailItem(
                        candidate.ExceptionId,
                        candidate.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        "Forcepoint POST yapildi ancak dogrulama cevabi JSON olarak okunamadi.",
                        verifyBody));
                }
                continue;
            }

            var verifyPayload = verifyPayloadRoot == null ? null : SelectForcepointPayload(verifyPayloadRoot);
            var verifyNames = verifyPayload == null
                ? new List<string>()
                : ExtractTopLevelExceptionRuleNames(verifyPayload);
            var verifiedEnabledByException = foundItems.ToDictionary(
                item => item.ExceptionRuleName,
                item => verifyPayload == null ? string.Empty : GetExceptionEnabled(verifyPayload, item.ExceptionRuleName) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
            var verificationHasSameExceptionSet = HasSameValues(originalNames, verifyNames);
            var verificationHasRequestedEnabled = verifiedEnabledByException.Values.All(value =>
                string.Equals(value, requestedEnabled, StringComparison.OrdinalIgnoreCase));
            var verificationSucceeded = verifyPayload != null &&
                                        verifyNames.Count > 0 &&
                                        verificationHasSameExceptionSet &&
                                        verificationHasRequestedEnabled;

            if (!verificationSucceeded)
            {
                var message = BuildVerificationFailureMessage(
                    verifyPayload != null && verifyNames.Count > 0,
                    verificationHasSameExceptionSet,
                    verificationHasRequestedEnabled);
                _logger.LogWarning(
                    "Forcepoint exception verification failed after POST. Policy={PolicyName}, Rule={RuleName}, Count={Count}, Message={Message}, VerifiedEnabled={VerifiedEnabled}, Body={Body}",
                    policyName,
                    ruleName,
                    foundItems.Count,
                    message,
                    JsonSerializer.Serialize(verifiedEnabledByException),
                    Truncate(verifyBody, 2000));

                foreach (var candidate in foundItems)
                {
                    items.Add(FailItem(
                        candidate.ExceptionId,
                        candidate.PolicyName,
                        ruleName,
                        candidate.ExceptionRuleName,
                        enabled,
                        message,
                        verifyBody));
                }
                continue;
            }

            var syncedEntriesForRule = await _context.PolicyRuleExceptions
                .Where(e => e.RuleName == ruleName)
                .ToListAsync(ct);

            foreach (var candidate in foundItems)
            {
                if (candidate.Entity != null)
                {
                    candidate.Entity.Enabled = enabled ? "true" : "false";
                    candidate.Entity.UpdatedAt = DateTime.UtcNow;
                    updatedEntities.Add(candidate.Entity);
                }

                var matchingSyncedEntries = syncedEntriesForRule.Where(e =>
                    string.Equals(e.PolicyName, candidate.PolicyName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.ExceptionName, candidate.ExceptionRuleName, StringComparison.OrdinalIgnoreCase));

                foreach (var syncedEntry in matchingSyncedEntries)
                {
                    syncedEntry.Enabled = enabled ? "true" : "false";
                    syncedEntry.SyncedAt = DateTime.UtcNow;
                    updatedSyncedExceptions.Add(syncedEntry);
                }

                items.Add(new ForcepointExceptionBulkToggleItemResult(
                    true,
                    enabled ? "Exception Forcepoint uzerinde aktif edildi." : "Exception Forcepoint uzerinde pasif edildi.",
                    candidate.ExceptionId,
                    candidate.PolicyName,
                    ruleName,
                    candidate.ExceptionRuleName,
                    enabled ? "true" : "false",
                    postBody));
            }
        }

        if (updatedEntities.Count > 0 || updatedSyncedExceptions.Count > 0)
            await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Forcepoint bulk exception enabled changed. Requested={Requested}, Updated={Updated}, Failed={Failed}, Enabled={Enabled}, Actor={Actor}",
            requestedCount, items.Count(i => i.Success), items.Count(i => !i.Success), enabled, actor);

        return BuildBulkResult(requestedCount, enabled, items);
    }

    private static ForcepointExceptionToggleCandidate? CreateCandidate(
        PIException exception,
        bool enabled,
        List<ForcepointExceptionBulkToggleItemResult> items)
    {
        var ruleName = exception.Rule?.RuleName;
        var exceptionName = exception.ExceptionRuleName;
        if (!string.IsNullOrWhiteSpace(ruleName) && !string.IsNullOrWhiteSpace(exceptionName))
        {
            return new ForcepointExceptionToggleCandidate(
                exception.Id,
                exception.Rule?.Policy?.PolicyName,
                ruleName,
                exceptionName,
                exception);
        }

        items.Add(FailItem(
            exception.Id,
            exception.Rule?.Policy?.PolicyName,
            ruleName,
            exceptionName,
            enabled,
            "Rule veya exception adi bos oldugu icin Forcepoint guncellemesi yapilamadi."));
        return null;
    }

    private static HttpRequestMessage CreateGetExceptionsRequest(HttpClient httpClient, string ruleName, string? policyName, string token)
    {
        var baseUri = httpClient.BaseAddress ?? throw new InvalidOperationException("DLP API base address is not configured.");
        var authority = baseUri.GetLeftPart(UriPartial.Authority);
        var endpoint = $"{authority}//dlp/rest/v1/policy/rules/exceptions?type={Uri.EscapeDataString(PolicyType)}&ruleName={Uri.EscapeDataString(ruleName)}";
        if (!string.IsNullOrWhiteSpace(policyName))
            endpoint += $"&policyName={Uri.EscapeDataString(policyName)}";
        var getUri = new Uri(endpoint);
        var request = new HttpRequestMessage(HttpMethod.Get, getUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task<ForcepointPostAttemptResult> SendPostExceptionsAsync(
        HttpClient httpClient,
        string token,
        string json,
        CancellationToken ct)
    {
        ForcepointPostAttemptResult? lastAttempt = null;
        foreach (var useDoubleSlash in new[] { true, false })
        {
            using var request = CreatePostExceptionsRequest(httpClient, token, json, useDoubleSlash);
            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var attempt = new ForcepointPostAttemptResult(
                response.IsSuccessStatusCode,
                request.RequestUri,
                response.StatusCode,
                response.ReasonPhrase,
                body);

            if (attempt.Success)
                return attempt;

            lastAttempt = attempt;
        }

        return lastAttempt ?? new ForcepointPostAttemptResult(
            false,
            null,
            System.Net.HttpStatusCode.InternalServerError,
            "No POST attempt was executed.",
            string.Empty);
    }

    private static HttpRequestMessage CreatePostExceptionsRequest(
        HttpClient httpClient,
        string token,
        string json,
        bool useDoubleSlash)
    {
        var baseUri = httpClient.BaseAddress ?? throw new InvalidOperationException("DLP API base address is not configured.");
        var authority = baseUri.GetLeftPart(UriPartial.Authority);
        var path = useDoubleSlash ? "//dlp/rest/v1/policy/rules/exceptions" : "/dlp/rest/v1/policy/rules/exceptions";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{authority}{path}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    private static ForcepointExceptionBulkToggleResult EmptyBulkResult()
    {
        return new ForcepointExceptionBulkToggleResult(
            false,
            "Islem yapilacak exception secilmedi.",
            0,
            0,
            0,
            Array.Empty<ForcepointExceptionBulkToggleItemResult>());
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

    private static void EnsurePostMetadata(JsonNode payload, string? policyName, string ruleName)
    {
        if (payload is JsonObject obj)
        {
            if (!string.IsNullOrWhiteSpace(policyName))
                obj["parent_policy_name"] ??= policyName;
            obj["parent_rule_name"] ??= ruleName;
            obj["policy_type"] ??= PolicyType;
        }
    }

    private static void SanitizePayloadForPost(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keysToRemove = obj
                .Select(property => property.Key)
                .Where(key => key.StartsWith("dup_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
                obj.Remove(key);

            foreach (var property in obj.ToList())
            {
                if (property.Value != null)
                    SanitizePayloadForPost(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                    SanitizePayloadForPost(item);
            }
        }
    }

    private static List<string> ExtractTopLevelExceptionRuleNames(JsonNode payload)
    {
        var names = new List<string>();
        if (payload is not JsonObject obj ||
            obj["exception_rules"] is not JsonArray exceptionRules)
        {
            return names;
        }

        foreach (var item in exceptionRules)
        {
            if (item is not JsonObject exceptionObj) continue;
            var name = GetJsonNodeString(exceptionObj, "exception_rule_name", "exceptionRuleName", "exception_name", "exceptionName");
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }

        return names;
    }

    private static string? GetExceptionEnabled(JsonNode payload, string exceptionName)
    {
        var exceptionNode = FindExceptionNode(payload, exceptionName);
        return exceptionNode == null
            ? null
            : GetJsonNodeString(exceptionNode, "enabled", "Enabled", "is_enabled", "isEnabled");
    }

    private static string? GetJsonNodeString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var node) &&
                node is JsonValue value &&
                value.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    private static bool HasSameValues(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left
            .Select(NormalizeKey)
            .OrderBy(v => v)
            .SequenceEqual(right.Select(NormalizeKey).OrderBy(v => v));
    }

    private static string BuildUnsafePreviewMessage(
        IReadOnlyList<string> missingTargets,
        bool hasNonEmptyExceptionList,
        bool hasSameExceptionSet,
        bool hasRequestedEnabled)
    {
        var reasons = new List<string>();
        if (missingTargets.Count > 0)
            reasons.Add($"hedef exception bulunamadi: {string.Join(", ", missingTargets)}");
        if (!hasNonEmptyExceptionList)
            reasons.Add("exception_rules bos veya okunamadi");
        if (!hasSameExceptionSet)
            reasons.Add("POST preview exception isim listesini korumuyor");
        if (!hasRequestedEnabled)
            reasons.Add("hedef exception enabled degeri istenen degere donusmedi");

        return $"Preview guvenli degil: {string.Join("; ", reasons)}.";
    }

    private static string BuildVerificationFailureMessage(
        bool hasNonEmptyExceptionList,
        bool hasSameExceptionSet,
        bool hasRequestedEnabled)
    {
        var reasons = new List<string>();
        if (!hasNonEmptyExceptionList)
            reasons.Add("dogrulama GET cevabinda exception_rules bos veya okunamadi");
        if (!hasSameExceptionSet)
            reasons.Add("dogrulama GET cevabi exception isim listesini korumuyor");
        if (!hasRequestedEnabled)
            reasons.Add("hedef exception enabled degeri Forcepoint uzerinde istenen degere donusmedi");

        return $"Forcepoint POST yapildi ancak dogrulama basarisiz: {string.Join("; ", reasons)}.";
    }

    private static string? GetString(Dictionary<string, JsonElement> source, string key)
    {
        return source.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string BuildRefKey(string? policyName, string? ruleName, string? exceptionName)
    {
        return $"{NormalizeKey(policyName)}|{NormalizeKey(ruleName)}|{NormalizeKey(exceptionName)}";
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value ?? string.Empty
            : value[..maxLength];

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
