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
        List<string>? iobs = null)
    {
        return new IncidentResponse
        {
            // ── Core fields ─────────────────────────────────────────────────
            Id                = incident.Id,
            UserEmail         = GetValidUserIdentifier(incident.UserEmail, incident.EmailAddress, incident.FullName),
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
            LoginName         = GetValidUserIdentifier(incident.LoginName, incident.EmailAddress, incident.FullName),
            HostName          = incident.HostName,
            EmailAddress      = incident.EmailAddress,
            ViolationTriggers = incident.ViolationTriggers,
            FullName          = incident.FullName,
            Team              = incident.Team,

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
    /// Returns the best available user identifier. If the primary value is null, empty, 
    /// or "unknown", it falls back to emailAddress, then fullName.
    /// </summary>
    private static string? GetValidUserIdentifier(string? primary, string? emailAddress, string? fullName)
    {
        bool isInvalid = string.IsNullOrWhiteSpace(primary) || 
                         primary.Equals("unknown", StringComparison.OrdinalIgnoreCase);
        
        if (!isInvalid) return primary;

        if (!string.IsNullOrWhiteSpace(emailAddress)) return emailAddress;
        if (!string.IsNullOrWhiteSpace(fullName)) return fullName;
        
        return primary; // fallback to the original value (e.g. "unknown" or null)
    }
}
