namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Request model for manual incident collection from the dashboard.
/// Supports two modes: date-based and hour-based (lookback).
/// </summary>
public class ManualCollectRequest
{
    /// <summary>Date-based mode: start date (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss)</summary>
    public string? StartDate { get; set; }

    /// <summary>Date-based mode: end date (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss)</summary>
    public string? EndDate { get; set; }

    /// <summary>Hour-based mode: lookback hours from now (e.g. 12 = last 12 hours)</summary>
    public int? LookbackHours { get; set; }
}

/// <summary>
/// Command published to Redis pub/sub to trigger manual collection in the Collector service.
/// </summary>
public class ManualCollectCommand
{
    public string JobId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Status of a manual collection job, stored in Redis for progress tracking.
/// </summary>
public class ManualCollectStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = ManualCollectStatusValues.Queued;
    public int Progress { get; set; }
    public string? Message { get; set; }
    public int TotalIncidents { get; set; }
    public int CurrentChunk { get; set; }
    public int TotalChunks { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Constants for manual collection job status values.
/// </summary>
public static class ManualCollectStatusValues
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
