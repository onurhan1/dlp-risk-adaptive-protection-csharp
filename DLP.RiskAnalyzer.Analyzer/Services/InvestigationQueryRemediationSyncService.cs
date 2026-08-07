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

            var email = record.MailAddress.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) continue;

            var localPart = email.Split('@', 2)[0];
            var loginSuffix = "\\" + localPart;

            var incidents = await _context.Incidents
                .Where(i =>
                    (i.UserEmail != null && (
                        i.UserEmail.ToLower() == email ||
                        i.UserEmail.ToLower() == localPart ||
                        i.UserEmail.ToLower().EndsWith(loginSuffix))) ||
                    (i.EmailAddress != null && i.EmailAddress.ToLower() == email) ||
                    (i.LoginName != null && (
                        i.LoginName.ToLower() == localPart ||
                        i.LoginName.ToLower().EndsWith(loginSuffix))))
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
}
