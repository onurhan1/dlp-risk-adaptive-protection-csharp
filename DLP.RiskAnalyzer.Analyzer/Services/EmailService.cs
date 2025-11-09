using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Email Service - Send emails via SMTP
/// </summary>
public class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string? _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _smtpUsername;
    private readonly string? _smtpPassword;
    private readonly bool _smtpEnableSsl;
    private readonly string? _fromEmail;
    private readonly string? _fromName;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _smtpHost = _configuration["Email:SmtpHost"];
        _smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
        _smtpUsername = _configuration["Email:SmtpUsername"];
        _smtpPassword = _configuration["Email:SmtpPassword"];
        _smtpEnableSsl = _configuration.GetValue<bool>("Email:SmtpEnableSsl", true);
        _fromEmail = _configuration["Email:FromEmail"];
        _fromName = _configuration["Email:FromName"] ?? "DLP Risk Analyzer";
    }

    /// <summary>
    /// Check if email service is configured
    /// </summary>
    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_smtpHost) &&
               !string.IsNullOrEmpty(_smtpUsername) &&
               !string.IsNullOrEmpty(_smtpPassword) &&
               !string.IsNullOrEmpty(_fromEmail);
    }

    /// <summary>
    /// Send email
    /// </summary>
    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        bool isHtml = true,
        string? toName = null)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("Email service is not configured. SMTP settings missing.");
            return false;
        }

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpEnableSsl,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 30000 // 30 seconds
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail!, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            message.To.Add(new MailAddress(toEmail, toName ?? toEmail));

            await client.SendMailAsync(message);
            
            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error sending email to {ToEmail}", toEmail);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {ToEmail}", toEmail);
            return false;
        }
    }

    /// <summary>
    /// Send test email
    /// </summary>
    public async Task<bool> SendTestEmailAsync(string toEmail)
    {
        var subject = "DLP Risk Analyzer - Test Email";
        var body = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #00a8e8 0%, #0066cc 100%); color: white; padding: 20px; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; padding: 12px 24px; background: #00a8e8; color: white; text-decoration: none; border-radius: 4px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Forcepoint DLP Risk Analyzer</h1>
        </div>
        <div class=""content"">
            <h2>Test Email</h2>
            <p>This is a test email from the DLP Risk Analyzer system.</p>
            <p>If you received this email, your email configuration is working correctly.</p>
            <p><strong>Sent at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            <p>You can now receive real-time notifications for high-risk incidents.</p>
        </div>
    </div>
</body>
</html>";

        return await SendEmailAsync(toEmail, subject, body, isHtml: true);
    }
}

