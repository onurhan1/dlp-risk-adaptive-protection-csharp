using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Anomaly Detection model for storing detected anomalies
/// </summary>
[Table("anomaly_detections")]
public class AnomalyDetection
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_email")]
    public string UserEmail { get; set; } = string.Empty;

    [Column("metric_type")]
    public string MetricType { get; set; } = string.Empty;

    [Column("current_value")]
    public double CurrentValue { get; set; }

    [Column("baseline_mean")]
    public double BaselineMean { get; set; }

    [Column("baseline_std_dev")]
    public double BaselineStdDev { get; set; }

    [Column("anomaly_score")]
    public int AnomalyScore { get; set; }

    [Column("severity")]
    public string Severity { get; set; } = string.Empty;

    [Column("timestamp")]
    public DateTime Timestamp { get; set; }
}

