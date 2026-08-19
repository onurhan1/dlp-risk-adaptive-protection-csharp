using DLP.RiskAnalyzer.Analyzer.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IDirectorySettingsService
{
    Task<ImapSettingsResponse> GetImapAsync(CancellationToken ct = default);
    Task<ImapSettingsResponse> SaveImapAsync(ImapSettingsRequest request, CancellationToken ct = default);
    Task<DirectorySettingsTestResult> TestImapAsync(ImapSettingsRequest request, CancellationToken ct = default);
    Task<ImapInboxPreviewResponse> PreviewInboxAsync(ImapInboxRequest request, CancellationToken ct = default);
    Task<ImapMessageContentResponse> GetInboxMessageAsync(ImapMessageContentRequest request, CancellationToken ct = default);
    Task<LdapSettingsResponse> GetLdapAsync(CancellationToken ct = default);
    Task<LdapSettingsResponse> SaveLdapAsync(LdapSettingsRequest request, CancellationToken ct = default);
    Task<DirectorySettingsTestResult> TestLdapAsync(LdapSettingsRequest request, CancellationToken ct = default);
    Task<LdapAuthenticationResult> AuthenticateLdapAsync(string username, string password, CancellationToken ct = default);
    Task<LdapUserLookupResult> LookupLdapUserAsync(string username, CancellationToken ct = default);
}
