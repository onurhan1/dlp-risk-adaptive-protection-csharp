using System.Net;
using System.Net.Mail;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class EmailConfigurationService
{
    private const string HostKey = "email_smtp_host";
    private const string PortKey = "email_smtp_port";
    private const string UsernameKey = "email_smtp_username";
    private const string PasswordKey = "email_smtp_password_protected";
    private const string EnableSslKey = "email_smtp_enable_ssl";
    private const string FromEmailKey = "email_from_email";
    private const string FromNameKey = "email_from_name";

    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailConfigurationService> _logger;

    public EmailConfigurationService(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ILogger<EmailConfigurationService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("Email.SmtpSecretProtector");
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<EmailSettingsResponse> GetAsync(bool includeSensitive = false, CancellationToken cancellationToken = default)
    {
        var settings = await _context.SystemSettings.AsNoTracking()
            .Where(s => s.Key.StartsWith("email_", StringComparison.OrdinalIgnoreCase))
            .ToListAsync(cancellationToken);

        var dict = settings.ToDictionary(s => s.Key, s => s);
        var response = BuildResponse(dict);

        if (includeSensitive)
        {
            var password = dict.TryGetValue(PasswordKey, out var pwSetting)
                ? TryUnprotect(pwSetting.Value)
                : _configuration["Email:SmtpPassword"];

            return new EmailSettingsSensitiveResponse
            {
                SmtpHost = response.SmtpHost,
                SmtpPort = response.SmtpPort,
                EnableSsl = response.EnableSsl,
                Username = response.Username,
                PasswordSet = response.PasswordSet,
                FromEmail = response.FromEmail,
                FromName = response.FromName,
                IsConfigured = response.IsConfigured,
                UpdatedAt = response.UpdatedAt,
                Password = password ?? string.Empty
            };
        }

        return response;
    }

    public async Task<EmailSettingsResponse> SaveAsync(EmailSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await UpsertAsync(HostKey, request.SmtpHost.Trim(), cancellationToken);
        await UpsertAsync(PortKey, request.SmtpPort.ToString(), cancellationToken);
        await UpsertAsync(EnableSslKey, request.EnableSsl.ToString(), cancellationToken);
        await UpsertAsync(UsernameKey, request.Username.Trim(), cancellationToken);
        await UpsertAsync(FromEmailKey, request.FromEmail.Trim(), cancellationToken);
        await UpsertAsync(FromNameKey, request.FromName.Trim(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var protectedPassword = _protector.Protect(request.Password);
            await UpsertAsync(PasswordKey, protectedPassword, cancellationToken);
        }

        return await GetAsync(false, cancellationToken);
    }

    public async Task<EmailConfigTestResult> TestAsync(EmailSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, allowEmptyPassword: false);

        try
        {
            using var client = new SmtpClient(request.SmtpHost, request.SmtpPort)
            {
                EnableSsl = request.EnableSsl,
                Credentials = new NetworkCredential(request.Username, request.Password),
                Timeout = 15000
            };

            // Use NOOP (if supported) by sending empty message to self
            await client.SendMailAsync(new MailMessage
            {
                From = new MailAddress(request.FromEmail, request.FromName),
                To = { new MailAddress(request.FromEmail, request.FromName) },
                Subject = "SMTP Test - Forcepoint DLP Risk Analyzer",
                Body = "This is an SMTP connectivity test message.",
                IsBodyHtml = false
            });

            return new EmailConfigTestResult
            {
                Success = true,
                Message = "SMTP connection successful. Test email sent.",
                TestedAt = DateTime.UtcNow
            };
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP test failed");
            return new EmailConfigTestResult
            {
                Success = false,
                Message = $"SMTP error: {ex.Message}",
                TestedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP test failed");
            return new EmailConfigTestResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                TestedAt = DateTime.UtcNow
            };
        }
    }

    private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
    {
        var entity = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (entity == null)
        {
            entity = new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(entity);
        }
        else
        {
            entity.Value = value;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.SystemSettings.Update(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private EmailSettingsResponse BuildResponse(Dictionary<string, SystemSetting> dict)
    {
        string? Get(string key, string? fallback = null) =>
            dict.TryGetValue(key, out var setting) ? setting.Value : fallback;

        var smtpHost = Get(HostKey, _configuration["Email:SmtpHost"]) ?? string.Empty;
        var smtpPort = int.TryParse(Get(PortKey, _configuration["Email:SmtpPort"]), out var port)
            ? port
            : 587;
        var enableSsl = bool.TryParse(Get(EnableSslKey, _configuration["Email:SmtpEnableSsl"]), out var ssl)
            ? ssl
            : true;
        var username = Get(UsernameKey, _configuration["Email:SmtpUsername"]) ?? string.Empty;
        var fromEmail = Get(FromEmailKey, _configuration["Email:FromEmail"]) ?? string.Empty;
        var fromName = Get(FromNameKey, _configuration["Email:FromName"]) ?? "DLP Risk Analyzer";

        var passwordSet = dict.ContainsKey(PasswordKey) || !string.IsNullOrEmpty(_configuration["Email:SmtpPassword"]);

        return new EmailSettingsResponse
        {
            SmtpHost = smtpHost,
            SmtpPort = smtpPort,
            EnableSsl = enableSsl,
            Username = username,
            PasswordSet = passwordSet,
            FromEmail = fromEmail,
            FromName = fromName,
            IsConfigured = !string.IsNullOrEmpty(smtpHost) &&
                           !string.IsNullOrEmpty(username) &&
                           passwordSet &&
                           !string.IsNullOrEmpty(fromEmail),
            UpdatedAt = dict.Values.OrderByDescending(s => s.UpdatedAt).FirstOrDefault()?.UpdatedAt
        };
    }

    private string? TryUnprotect(string value)
    {
        try
        {
            return _protector.Unprotect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt stored SMTP password");
            return null;
        }
    }

    private static void ValidateRequest(EmailSettingsRequest request, bool allowEmptyPassword = true)
    {
        if (string.IsNullOrWhiteSpace(request.SmtpHost))
        {
            throw new ArgumentException("SMTP host is required");
        }

        if (request.SmtpPort < 1 || request.SmtpPort > 65535)
        {
            throw new ArgumentException("SMTP port must be between 1 and 65535");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("SMTP username is required");
        }

        if (!allowEmptyPassword && string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("SMTP password is required");
        }

        if (string.IsNullOrWhiteSpace(request.FromEmail))
        {
            throw new ArgumentException("From email is required");
        }
    }
}

