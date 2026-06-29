using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IEmailConfigurationService
{
    Task<EmailSettingsResponse> GetAsync(bool includeSensitive = false, CancellationToken cancellationToken = default);
    Task<EmailSettingsResponse> SaveAsync(EmailSettingsRequest request, CancellationToken cancellationToken = default);
    Task<EmailConfigTestResult> TestAsync(EmailSettingsRequest request, CancellationToken cancellationToken = default);
}
