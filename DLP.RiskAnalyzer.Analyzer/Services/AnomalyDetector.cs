using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Anomaly Detection Service - User baseline calculation and anomaly detection
/// </summary>
public class AnomalyDetector
{
    private readonly AnalyzerDbContext _context;
    private readonly int _baselineDays = 20; // 20-day rolling baseline

    public AnomalyDetector(AnalyzerDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Calculate user baseline (mean and standard deviation)
    /// </summary>
    public async Task<Dictionary<string, double>> CalculateUserBaselineAsync(
        string userEmail, 
        string metricType)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-_baselineDays);

        var query = metricType switch
        {
            "cloud_upload" => _context.Incidents
                .Where(i => i.UserEmail == userEmail &&
                           i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                           i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue) &&
                           (i.Channel == "Cloud" || i.Channel == "Cloud Storage"))
                .GroupBy(i => DateOnly.FromDateTime(i.Timestamp.Date))
                .Select(g => g.Count()),

            "email_count" => _context.Incidents
                .Where(i => i.UserEmail == userEmail &&
                           i.Channel == "Email" &&
                           i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                           i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
                .GroupBy(i => DateOnly.FromDateTime(i.Timestamp.Date))
                .Select(g => g.Count()),

            "file_copy" => _context.Incidents
                .Where(i => i.UserEmail == userEmail &&
                           (i.Channel == "Network Share" || i.Channel == "Removable Storage") &&
                           i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                           i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
                .GroupBy(i => DateOnly.FromDateTime(i.Timestamp.Date))
                .Select(g => g.Count()),

            _ => _context.Incidents
                .Where(i => i.UserEmail == userEmail &&
                           i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                           i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
                .GroupBy(i => DateOnly.FromDateTime(i.Timestamp.Date))
                .Select(g => g.Count())
        };

        var values = await query.ToListAsync();

        if (values.Count == 0)
        {
            return new Dictionary<string, double>
            {
                { "mean", 0.0 },
                { "std_dev", 0.0 },
                { "min", 0.0 },
                { "max", 0.0 },
                { "sample_count", 0.0 }
            };
        }

        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        var stdDev = Math.Sqrt(variance);

        return new Dictionary<string, double>
        {
            { "mean", mean },
            { "std_dev", stdDev },
            { "min", values.Min() },
            { "max", values.Max() },
            { "sample_count", values.Count }
        };
    }

    /// <summary>
    /// Detect anomalies using 3-sigma rule
    /// </summary>
    public async Task<Dictionary<string, object>> DetectAnomaliesAsync(
        string userEmail,
        double currentValue,
        string metricType = "cloud_upload")
    {
        var baseline = await CalculateUserBaselineAsync(userEmail, metricType);
        var mean = baseline["mean"];
        var stdDev = baseline["std_dev"];

        if (stdDev == 0 || baseline["sample_count"] < 5)
        {
            return new Dictionary<string, object>
            {
                { "is_anomaly", false },
                { "baseline_mean", mean },
                { "baseline_std_dev", stdDev },
                { "current_value", currentValue },
                { "anomaly_score", 0 },
                { "severity", "None" },
                { "message", "Insufficient baseline data" }
            };
        }

        // 3-sigma rule: value > mean + 3*std_dev is anomaly
        var threshold = mean + (3 * stdDev);
        var isAnomaly = currentValue > threshold;

        if (!isAnomaly)
        {
            return new Dictionary<string, object>
            {
                { "is_anomaly", false },
                { "baseline_mean", mean },
                { "baseline_std_dev", stdDev },
                { "current_value", currentValue },
                { "anomaly_score", 0 },
                { "severity", "None" },
                { "message", "No anomaly detected" }
            };
        }

        // Calculate anomaly score (0-100)
        var deviation = currentValue - mean;
        var sigmaMultiples = deviation / stdDev;
        var anomalyScore = Math.Min(100, (int)(sigmaMultiples * 10));

        // Determine severity
        var severity = sigmaMultiples switch
        {
            >= 5 => "High",
            >= 3 => "Medium",
            _ => "Low"
        };

        return new Dictionary<string, object>
        {
            { "is_anomaly", true },
            { "baseline_mean", mean },
            { "baseline_std_dev", stdDev },
            { "current_value", currentValue },
            { "anomaly_score", anomalyScore },
            { "severity", severity },
            { "sigma_multiples", sigmaMultiples },
            { "message", $"Anomaly detected: {currentValue} is {sigmaMultiples:F1}σ above baseline ({mean:F1})" }
        };
    }

    /// <summary>
    /// Save anomaly detection to database
    /// </summary>
    public async Task<int> SaveAnomalyDetectionAsync(
        string userEmail,
        string metricType,
        double currentValue,
        double baselineMean,
        int anomalyScore,
        string severity)
    {
        // Note: Requires anomaly_detections table in database
        // This is a placeholder - implement based on your database schema
        return 0;
    }

    /// <summary>
    /// Get anomaly detections
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetAnomalyDetectionsAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        string? severity)
    {
        // Note: Requires anomaly_detections table
        // Placeholder implementation
        return new List<Dictionary<string, object>>();
    }
}

