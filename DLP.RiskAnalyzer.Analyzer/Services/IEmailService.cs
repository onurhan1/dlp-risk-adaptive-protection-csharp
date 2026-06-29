namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IEmailService
{
    Task<bool> IsConfiguredAsync();
    Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, string? toName = null, string? ccEmail = null);
    Task<bool> SendTestEmailAsync(string toEmail);
}
