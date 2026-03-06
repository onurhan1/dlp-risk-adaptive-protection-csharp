namespace DLP.RiskAnalyzer.Shared.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Request model for manual incident collection from the dashboard.
/// Supports two modes: date-based and hour-based (lookback).
/// </summary>
public class ManualCollectRequest
{
    /// <summary>Date-based mode: start date (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss)</summary>
    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }

    /// <summary>Date-based mode: end date (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss)</summary>
    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }

    /// <summary>Hour-based mode: lookback hours from now (e.g. 12 = last 12 hours)</summary>
    [JsonPropertyName("lookback_hours")]
    public int? LookbackHours { get; set; }
}

/// <summary>
/// Command published to Redis pub/sub to trigger manual collection in the Collector service.
/// </summary>
public class ManualCollectCommand
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Status of a manual collection job, stored in Redis for progress tracking.
/// </summary>
public class ManualCollectStatus
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = ManualCollectStatusValues.Queued;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("total_incidents")]
    public int TotalIncidents { get; set; }

    [JsonPropertyName("current_chunk")]
    public int CurrentChunk { get; set; }

    [JsonPropertyName("total_chunks")]
    public int TotalChunks { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
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
