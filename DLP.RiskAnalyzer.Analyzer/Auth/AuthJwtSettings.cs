namespace DLP.RiskAnalyzer.Analyzer.Auth;

/// <summary>
/// JWT settings loaded from the database (auth.auth_settings) instead of appsettings.
/// Registered as a singleton at startup.
/// </summary>
public class AuthJwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "DLP-RiskAnalyzer";
    public string Audience { get; set; } = "DLP-RiskAnalyzer-Client";
    public int ExpirationHours { get; set; } = 8;
}
