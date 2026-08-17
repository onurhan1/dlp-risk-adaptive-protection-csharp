namespace DLP.RiskAnalyzer.Analyzer.Models;

public class ImapSettingsRequest
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Folder { get; set; } = "INBOX";
    public bool UnreadOnly { get; set; } = true;
    public int LookbackDays { get; set; } = 7;
    public int MaxMessages { get; set; } = 500;
}

public class ImapSettingsResponse
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public bool PasswordSet { get; set; }
    public string Folder { get; set; } = "INBOX";
    public bool UnreadOnly { get; set; } = true;
    public int LookbackDays { get; set; } = 7;
    public int MaxMessages { get; set; } = 500;
    public bool IsConfigured { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class LdapSettingsRequest
{
    public bool Enabled { get; set; }
    public bool UseLdaps { get; set; } = true;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 636;
    public string Domain { get; set; } = string.Empty;
    public string SearchBase { get; set; } = string.Empty;
    public string ServiceAccount { get; set; } = string.Empty;
    public string? ServicePassword { get; set; }
    public string UserFilter { get; set; } = "(sAMAccountName={0})";
    public string AdminGroup { get; set; } = string.Empty;
    public string StandardGroup { get; set; } = string.Empty;
}

public class LdapSettingsResponse
{
    public bool Enabled { get; set; }
    public bool UseLdaps { get; set; } = true;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 636;
    public string Domain { get; set; } = string.Empty;
    public string SearchBase { get; set; } = string.Empty;
    public string ServiceAccount { get; set; } = string.Empty;
    public bool ServicePasswordSet { get; set; }
    public string UserFilter { get; set; } = "(sAMAccountName={0})";
    public string AdminGroup { get; set; } = string.Empty;
    public string StandardGroup { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DirectorySettingsTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}
