using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Helpers;

/// <summary>
/// Single factory for mapping an Incident entity + enrichment data to IncidentResponse.
/// Eliminates the duplicated mapping block that previously existed in both
/// GetIncidents() and GetIncident() in IncidentsController.
/// </summary>
public static class IncidentResponseMapper
{
    /// <summary>
    /// Maps an <see cref="Incident"/> entity to an <see cref="IncidentResponse"/> DTO,
    /// including optional enrichment data computed by the risk analyzer.
    /// </summary>
    /// <param name="incident">Source entity from the database.</param>
    /// <param name="riskLevel">Pre-computed risk level string (e.g. "High").</param>
    /// <param name="recommendedAction">Pre-computed policy action recommendation.</param>
    /// <param name="iobs">Pre-computed list of Indicators of Behavior codes.</param>
    public static IncidentResponse Map(
        Incident incident,
        string? riskLevel = null,
        string? recommendedAction = null,
        List<string>? iobs = null,
        string? userFullName = null,
        string? userEmailAddress = null,
        string? userTeam = null)
    {
        return new IncidentResponse
        {
            // ── Core fields ─────────────────────────────────────────────────
            Id                = incident.Id,
            UserEmail         = GetValidUserIdentifier(incident.UserEmail, userEmailAddress ?? incident.EmailAddress) ?? "unknown",
            Department        = incident.Department,
            Severity          = incident.Severity,
            DataType          = incident.DataType,
            Timestamp         = incident.Timestamp,
            Policy            = incident.Policy,
            RuleName          = incident.RuleName,
            Channel           = incident.Channel,
            RiskScore         = incident.RiskScore,
            RepeatCount       = incident.RepeatCount,
            DataSensitivity   = incident.DataSensitivity,
            MaxMatches        = incident.MaxMatches,

            // ── Extended DLP API fields ──────────────────────────────────────
            Action            = incident.Action,
            Destination       = incident.Destination,
            FileName          = incident.FileName,
            LoginName         = GetValidUserIdentifier(incident.LoginName, userEmailAddress ?? incident.EmailAddress),
            HostName          = incident.HostName,
            EmailAddress      = userEmailAddress ?? incident.EmailAddress,
            ViolationTriggers = incident.ViolationTriggers,
            FullName          = userFullName,
            ManagerName       = incident.FullName,
            Team              = userTeam ?? incident.Team,

            // ── Enriched / computed fields ───────────────────────────────────
            RiskLevel         = riskLevel,
            RecommendedAction = recommendedAction,
            IOBs              = iobs,

            // ── Remediation fields ───────────────────────────────────────────
            IsRemediated      = incident.IsRemediated,
            RemediatedAt      = incident.RemediatedAt,
            RemediatedBy      = incident.RemediatedBy,
            RemediationAction = incident.RemediationAction,
            RemediationNotes  = incident.RemediationNotes,
        };
    }

    /// <summary>
    /// Tam yaniti Takim Bazli Analiz sayfasinin kullandigi alan setine indirger.
    /// Zenginlestirme mantigi tek yerde kalsin diye <see cref="Map"/> ciktisindan turetilir.
    /// </summary>
    public static IncidentCompactResponse MapCompact(IncidentResponse response) => new()
    {
        Id                = response.Id,
        Timestamp         = response.Timestamp,
        UserEmail         = response.UserEmail,
        Policy            = response.Policy,
        Action            = response.Action,
        Destination       = response.Destination,
        Department        = response.Department,
        Team              = response.Team,
        FullName          = response.FullName,
        MaxMatches        = response.MaxMatches,
        LoginName         = response.LoginName,
        EmailAddress      = response.EmailAddress,
        ViolationTriggers = response.ViolationTriggers,
        Channel           = response.Channel,
    };

    /// <summary>
    /// Returns the best available user identifier. If the primary value is null, empty, 
    /// or "unknown", it falls back to emailAddress, then fullName.
    /// </summary>
    private static string? GetValidUserIdentifier(string? primary, string? emailAddress)
    {
        bool isInvalid = string.IsNullOrWhiteSpace(primary) || 
                         primary.Equals("unknown", StringComparison.OrdinalIgnoreCase);
        
        if (!isInvalid) return primary;

        if (!string.IsNullOrWhiteSpace(emailAddress)) return emailAddress;
        
        return primary; // fallback to the original value (e.g. "unknown" or null)
    }
}
