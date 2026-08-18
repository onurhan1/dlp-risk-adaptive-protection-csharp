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

public class ImapInboxRequest : ImapSettingsRequest
{
    public int PreviewCount { get; set; } = 20;
}

public class ImapInboxMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public bool Unread { get; set; }
    public long Size { get; set; }
}

public class ImapInboxPreviewResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Folder { get; set; } = "INBOX";
    public int TotalMessages { get; set; }
    public int ReturnedMessages { get; set; }
    public List<ImapInboxMessageDto> Messages { get; set; } = new();
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
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
    public bool IsConfigured { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ExternalUserDbSettingsRequest
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "postgresql";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool Encrypt { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
    public string TableName { get; set; } = string.Empty;
    public string MatchColumn { get; set; } = "username";
    public string FirstNameColumn { get; set; } = string.Empty;
    public string LastNameColumn { get; set; } = string.Empty;
    public string FullNameColumn { get; set; } = string.Empty;
    public string EmailColumn { get; set; } = "email";
    public string DepartmentColumn { get; set; } = string.Empty;
    public string WhereClause { get; set; } = string.Empty;
}

public class ExternalUserDbSettingsResponse : ExternalUserDbSettingsRequest
{
    public bool PasswordSet { get; set; }
    public bool IsConfigured { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ExternalUserLookupRequest : ExternalUserDbSettingsRequest
{
    public string TestUsername { get; set; } = string.Empty;
}

public class ExternalUserProfileDto
{
    public string UserName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
}

public class ExternalUserLookupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ExternalUserProfileDto? User { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}

public class DirectorySettingsTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}
