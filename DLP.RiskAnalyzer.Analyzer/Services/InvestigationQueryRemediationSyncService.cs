using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class InvestigationQueryRemediationSyncService : IInvestigationQueryRemediationSyncService
{
    private static readonly HashSet<string> ProtectedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "resolved",
        "false_positive",
        "query_completed"
    };

    private readonly AnalyzerDbContext _context;
    private readonly ILogger<InvestigationQueryRemediationSyncService> _logger;

    public InvestigationQueryRemediationSyncService(
        AnalyzerDbContext context,
        ILogger<InvestigationQueryRemediationSyncService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SyncAsync(
        IEnumerable<InvestigationQueryRecord> records,
        string actor,
        DateTime syncAt,
        CancellationToken ct = default)
    {
        var updated = 0;

        foreach (var record in records)
        {
            var targetAction = ToRemediationAction(record.QueryStatus);
            if (targetAction == null) continue;

            var email = NormalizeLower(record.MailAddress);
            var fullName = NormalizeLower(record.FullName);
            var userCode = NormalizeUserCode(record.UserCode);
            if (string.IsNullOrWhiteSpace(userCode))
            {
                userCode = await ResolveUserCodeAsync(email, fullName, ct);
                if (!string.IsNullOrWhiteSpace(userCode))
                    record.UserCode = userCode;
            }

            var incidents = await _context.Incidents
                .Where(i =>
                    (!string.IsNullOrWhiteSpace(userCode) && (
                        (i.LoginName != null && (
                            i.LoginName.ToLower() == userCode ||
                            i.LoginName.ToLower().EndsWith("\\" + userCode))) ||
                        (i.UserEmail != null && i.UserEmail.ToLower() == userCode))) ||
                    (!string.IsNullOrWhiteSpace(email) && (
                        (i.UserEmail != null && i.UserEmail.ToLower() == email) ||
                        (i.EmailAddress != null && i.EmailAddress.ToLower() == email))) ||
                    (!string.IsNullOrWhiteSpace(fullName) &&
                        i.FullName != null && i.FullName.ToLower() == fullName))
                .ToListAsync(ct);

            foreach (var incident in incidents)
            {
                if (!CanUpdate(incident.RemediationAction, targetAction)) continue;

                var actionChanged = !string.Equals(incident.RemediationAction, targetAction, StringComparison.OrdinalIgnoreCase);
                incident.IsRemediated = true;
                incident.RemediationAction = targetAction;
                incident.RemediationNotes = BuildNotes(record, targetAction);
                incident.RemediatedBy = actor;
                incident.RemediatedAt = targetAction == "queried"
                    ? record.QueryDate ?? syncAt
                    : actionChanged || !incident.RemediatedAt.HasValue
                        ? syncAt
                        : incident.RemediatedAt;
                updated++;
            }
        }

        if (updated > 0)
        {
            _logger.LogInformation("Synced {Count} incidents from investigation query statuses", updated);
        }

        return updated;
    }

    private static string? ToRemediationAction(string status)
    {
        return NormalizeQueryStatus(status) switch
        {
            InvestigationQueryStatus.Queried => "queried",
            InvestigationQueryStatus.Completed => "query_completed",
            _ => null
        };
    }

    public static string NormalizeQueryStatus(string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "" => InvestigationQueryStatus.Pending,
            "pending" => InvestigationQueryStatus.Pending,
            "bekliyor" => InvestigationQueryStatus.Pending,
            "manuel" => InvestigationQueryStatus.Pending,
            "manual" => InvestigationQueryStatus.Pending,
            "queried" => InvestigationQueryStatus.Queried,
            "sorgulandi" => InvestigationQueryStatus.Queried,
            "sorgulandı" => InvestigationQueryStatus.Queried,
            "sorgu maili gonderildi" => InvestigationQueryStatus.Queried,
            "sorgu maili gönderildi" => InvestigationQueryStatus.Queried,
            "completed" => InvestigationQueryStatus.Completed,
            "complete" => InvestigationQueryStatus.Completed,
            "tamamlandi" => InvestigationQueryStatus.Completed,
            "tamamlandı" => InvestigationQueryStatus.Completed,
            "query_completed" => InvestigationQueryStatus.Completed,
            "sorgu sonuclandi" => InvestigationQueryStatus.Completed,
            "sorgu sonuçlandı" => InvestigationQueryStatus.Completed,
            _ => value
        };
    }

    private static bool CanUpdate(string? currentAction, string targetAction)
    {
        if (string.IsNullOrWhiteSpace(currentAction)) return true;
        if (string.Equals(currentAction, targetAction, StringComparison.OrdinalIgnoreCase)) return true;
        if (targetAction == "query_completed")
            return !ProtectedActions.Contains(currentAction) || string.Equals(currentAction, "queried", StringComparison.OrdinalIgnoreCase);

        return !ProtectedActions.Contains(currentAction);
    }

    private static string BuildNotes(InvestigationQueryRecord record, string targetAction)
    {
        var label = targetAction == "query_completed" ? "Sorgu tamamlandı" : "Sorgu maili gönderildi";
        var subject = string.IsNullOrWhiteSpace(record.Subject) ? "-" : record.Subject.Trim();
        var response = string.IsNullOrWhiteSpace(record.ResponseStatus) ? "-" : record.ResponseStatus.Trim();
        return $"{label}. Konu: {subject}. Kullanıcı dönüş durumu: {response}.";
    }

    private async Task<string> ResolveUserCodeAsync(string email, string fullName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var match = await _context.Incidents
            .AsNoTracking()
            .Where(i =>
                (!string.IsNullOrWhiteSpace(email) && (
                    (i.UserEmail != null && i.UserEmail.ToLower() == email) ||
                    (i.EmailAddress != null && i.EmailAddress.ToLower() == email))) ||
                (!string.IsNullOrWhiteSpace(fullName) &&
                    i.FullName != null && i.FullName.ToLower() == fullName))
            .OrderByDescending(i => i.Timestamp)
            .Select(i => new { i.LoginName, i.UserEmail })
            .FirstOrDefaultAsync(ct);

        var fromLogin = NormalizeUserCode(match?.LoginName);
        if (!string.IsNullOrWhiteSpace(fromLogin)) return fromLogin;

        return NormalizeUserCode(match?.UserEmail);
    }

    private static string NormalizeLower(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeUserCode(string? value)
    {
        var normalized = NormalizeLower(value);
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
        if (normalized.Contains('\\')) normalized = normalized.Split('\\').Last();
        if (normalized.Contains('@')) return string.Empty;
        if (normalized == "unknown" || normalized == "n/a") return string.Empty;
        return normalized;
    }
}
