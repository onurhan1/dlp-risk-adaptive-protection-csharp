namespace DLP.RiskAnalyzer.Analyzer.Models;

public class DlpApiSettingsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("manager_ip")]
    public string ManagerIp { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("manager_port")]
    public int ManagerPort { get; set; } = 8443;
    
    [System.Text.Json.Serialization.JsonPropertyName("use_https")]
    public bool UseHttps { get; set; } = true;
    
    [System.Text.Json.Serialization.JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 30;
    
    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class DlpApiSettingsResponse
{
    public string ManagerIp { get; set; } = string.Empty;
    public int ManagerPort { get; set; } = 8443;
    public bool UseHttps { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public string Username { get; set; } = string.Empty;
    public bool PasswordSet { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DlpApiSensitiveSettingsResponse : DlpApiSettingsResponse
{
    public string Password { get; set; } = string.Empty;
}

public class DlpApiTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? LatencyMs { get; set; }
    public int? StatusCode { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}

public class DlpConfigBroadcastMessage
{
    public string ManagerIp { get; set; } = string.Empty;
    public int ManagerPort { get; set; } = 8443;
    public bool UseHttps { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

