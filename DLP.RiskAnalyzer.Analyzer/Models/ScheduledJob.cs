namespace DLP.RiskAnalyzer.Analyzer.Models;

public class ScheduledJob
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string HandlerKey { get; set; } = string.Empty;
    public string? HandlerPayloadJson { get; set; }
    public string CronExpression { get; set; } = "0 2 * * *";
    public bool Enabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string LastStatus { get; set; } = ScheduledJobRunStatus.NeverRun;
    public string? LastMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ScheduledJobRun
{
    public int Id { get; set; }
    public int ScheduledJobId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string TriggerType { get; set; } = ScheduledJobTriggerType.Manual;
    public string Status { get; set; } = ScheduledJobRunStatus.Running;
    public string? Message { get; set; }
    public string? ResultJson { get; set; }
}

public static class ScheduledJobRunStatus
{
    public const string NeverRun = "never_run";
    public const string Running = "running";
    public const string Success = "success";
    public const string Failed = "failed";
}

public static class ScheduledJobTriggerType
{
    public const string Manual = "manual";
    public const string Schedule = "schedule";
}

public static class ScheduledJobHandlerKeys
{
    public const string ReleasedIncidentSync = "released_incident_sync";
    public const string PolicyExceptionSync = "policy_exception_sync";
    public const string IsolationForest = "isolation_forest";
    public const string LogCleanup = "log_cleanup";
    public const string WeeklyHighScoreUsersReport = "report_weekly_high_score_users";
    public const string TopPermitUsersReport = "report_top_permit_users";
    public const string TopBlockUsersReport = "report_top_block_users";
    public const string HighMaxMatchTransfersReport = "report_high_max_match_transfers";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [ReleasedIncidentSync] = "Released Incident Sync",
        [PolicyExceptionSync] = "Policy Exception Sync",
        [IsolationForest] = "Isolation Forest Analizi",
        [LogCleanup] = "Log Cleanup",
        [WeeklyHighScoreUsersReport] = "Rapor: Haftalık Yüksek Skorlu Kullanıcılar",
        [TopPermitUsersReport] = "Rapor: En Çok Permit Olay Kaydı Üretenler",
        [TopBlockUsersReport] = "Rapor: En Çok Block Olay Kaydı Üretenler",
        [HighMaxMatchTransfersReport] = "Rapor: Yüksek Maksimum Eşleşmeli Veri Gönderimleri"
    };

    public static bool IsReport(string handlerKey) =>
        handlerKey == WeeklyHighScoreUsersReport ||
        handlerKey == TopPermitUsersReport ||
        handlerKey == TopBlockUsersReport ||
        handlerKey == HighMaxMatchTransfersReport;
}
