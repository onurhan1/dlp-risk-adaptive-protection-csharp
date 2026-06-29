namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Remediation Service interface - Incident remediation via Forcepoint DLP API + Database storage
/// </summary>
public interface IRemediationService
{
    Task<Dictionary<string, object>> RemediateIncidentAsync(
        string incidentId,
        string action,
        string? reason = null,
        string? notes = null,
        string? remediatedBy = null);

    Task<Dictionary<string, object>> UpdateIncidentAsync(
        string incidentId,
        string? status = null,
        int? severity = null,
        string? assignedTo = null,
        string? notes = null,
        string? reason = null);
}
