namespace DLP.RiskAnalyzer.Analyzer.Models;

public class EmailSettingsRequest
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "DLP Risk Analyzer";
}

public class EmailSettingsResponse
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public bool PasswordSet { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "DLP Risk Analyzer";
    public bool IsConfigured { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EmailSettingsSensitiveResponse : EmailSettingsResponse
{
    public string Password { get; set; } = string.Empty;
}

public class EmailConfigTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}

