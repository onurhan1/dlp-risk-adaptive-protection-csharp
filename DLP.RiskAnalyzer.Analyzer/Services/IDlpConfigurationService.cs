using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IDlpConfigurationService
{
    Task<DlpApiSettingsResponse> GetAsync(bool includeSensitive = false, CancellationToken cancellationToken = default);
    Task<DlpApiSettingsResponse> SaveAsync(DlpApiSettingsRequest request, CancellationToken cancellationToken = default);
    Task<DlpApiTestResult> TestConnectionAsync(DlpApiSettingsRequest request, CancellationToken cancellationToken = default);
    Task<DlpApiSensitiveSettingsResponse> GetSensitiveConfigAsync(CancellationToken cancellationToken = default);
}
