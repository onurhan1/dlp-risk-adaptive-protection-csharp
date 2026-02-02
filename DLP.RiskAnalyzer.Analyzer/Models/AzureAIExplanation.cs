using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLP.RiskAnalyzer.Analyzer.Models
{
    /// <summary>
    /// Azure AI-generated explanations for DLP incidents
    /// Imported from external Azure AI analysis
    /// </summary>
    [Table("AI_Explanations")]
    public class AzureAIExplanation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("incident_id")]
        public int IncidentId { get; set; }

        [Required]
        [Column("user_email")]
        public string UserEmail { get; set; } = string.Empty;

        [Column("risk_level")]
        public string RiskLevel { get; set; } = string.Empty; // düşük, orta, yüksek, kritik

        [Column("risk_score")]
        public int RiskScore { get; set; }

        [Column("anomaly_detected")]
        public bool AnomalyDetected { get; set; }

        [Column("explanation")]
        public string Explanation { get; set; } = string.Empty;

        [Column("recommended_action")]
        public string RecommendedAction { get; set; } = string.Empty;

        [Column("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// DTO for Azure AI user summary (average scores)
    /// </summary>
    public class AzureAIUserSummary
    {
        public string UserEmail { get; set; } = string.Empty;
        public double AverageRiskScore { get; set; }
        public int TotalAnalyzedIncidents { get; set; }
        public int AnomalyCount { get; set; }
        public string HighestRiskLevel { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for Azure AI incident detail
    /// </summary>
    public class AzureAIIncidentDetail
    {
        public int IncidentId { get; set; }
        public int RiskScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public bool AnomalyDetected { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
