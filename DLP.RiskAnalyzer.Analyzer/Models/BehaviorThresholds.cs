namespace DLP.RiskAnalyzer.Analyzer.Models;

public static class BehaviorThresholds
{
    // Risk Thresholds
    public const int CriticalRiskThreshold = 85;
    public const int HighRiskThreshold = 65;
    public const int MediumRiskThreshold = 40;
    
    // Limits
    public const int MaxAICallEntities = 20;
    public const int ReferenceIncidentLimit = 10;
    public const int DestinationPatternLimit = 20;
    
    // Severity and Score Mins
    public const int HighSeverityMin = 7;
    public const int HighRiskIncidentMinScore = 50;
    
    // Action Threat Weights
    public const double ActionWeightBlock = 1.0;
    public const double ActionWeightQuarantine = 0.8;
    public const double ActionWeightAuthorized = 0.2;
    public const double ActionWeightReleased = 0.2;
    public const double ActionWeightDefault = 0.5;

    // Z-Score Threat Weights
    public const double HighThreatZScoreWeight = 1.0;
    public const double LowThreatZScoreWeight = 0.2;
}
