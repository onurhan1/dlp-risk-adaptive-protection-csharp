using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IExternalUserDirectoryService
{
    Task<ExternalUserDbSettingsResponse> GetSettingsAsync(CancellationToken ct = default);
    Task<ExternalUserDbSettingsResponse> SaveSettingsAsync(ExternalUserDbSettingsRequest request, CancellationToken ct = default);
    Task<ExternalUserLookupResult> TestConnectionAsync(ExternalUserDbSettingsRequest request, CancellationToken ct = default);
    Task<ExternalUserLookupResult> TestLookupAsync(ExternalUserLookupRequest request, CancellationToken ct = default);
    Task<ExternalUserProfileDto?> ResolveUserAsync(string? userName, CancellationToken ct = default);
}
