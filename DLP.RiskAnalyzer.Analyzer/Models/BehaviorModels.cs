using System.Collections.Generic;

namespace DLP.RiskAnalyzer.Analyzer.Models;

    public class BehaviorMetrics
    {
        public int TotalIncidents { get; set; }
        public double MeanIncidentsPerDay { get; set; }
        public double StdDevIncidentsPerDay { get; set; }
        public double AvgSeverity { get; set; }
        public double StdDevSeverity { get; set; }
        public Dictionary<string, int> ChannelCounts { get; set; } = new();
        // New metrics
        public Dictionary<string, int> ActionCounts { get; set; } = new();
        public int TotalMatches { get; set; }
        public double AvgMatchesPerIncident { get; set; }
        public double StdDevMatches { get; set; }
    }

    public class AnomalyResults
    {
        public double IncidentCountZScore { get; set; }
        public double SeverityZScore { get; set; }
        public double ChannelEmailZScore { get; set; }
        public double ChannelWebZScore { get; set; }
        public double ChannelEndpointZScore { get; set; }
        // New action-based Z-scores
        public double ActionBlockZScore { get; set; }
        public double ActionQuarantineZScore { get; set; }
        public double ActionAuthorizedZScore { get; set; }
        public double ActionReleasedZScore { get; set; }
        public double MaxMatchesZScore { get; set; }
        public double WeeklyTrendZScore { get; set; }
    }

