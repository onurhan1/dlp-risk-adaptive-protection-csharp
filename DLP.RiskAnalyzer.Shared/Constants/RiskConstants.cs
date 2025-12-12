namespace DLP.RiskAnalyzer.Shared.Constants;

/// <summary>
/// Centralized constants for Risk Analyzer application
/// </summary>
public static class RiskConstants
{
    public static class RiskScores
    {
        public const int CriticalThreshold = 91;
        public const int HighThreshold = 61;
        public const int MediumThreshold = 41;
        
        public const int MaxScore = 100;
        public const int MinScore = 0;
    }

    public static class Channels
    {
        public const string Cloud = "Cloud";
        public const string CloudStorage = "Cloud Storage";
        public const string Email = "Email";
        public const string NetworkShare = "Network Share";
        public const string RemovableStorage = "Removable Storage";
        public const string Printer = "Printer";
    }

    public static class MetricTypes
    {
        public const string CloudUpload = "cloud_upload";
        public const string EmailCount = "email_count";
        public const string FileCopy = "file_copy";
    }

    public static class DataSensitivity
    {
        public const string PII = "pii";
        public const string Personal = "personal";
        public const string PCI = "pci";
        public const string Credit = "credit";
        public const string Confidential = "confidential";
        
        // Data sensitivity thresholds for risk calculation
        public const int PIIThreshold = 8;
        public const int PCIThreshold = 9;
        public const int ConfidentialThreshold = 7;
    }
    
    public static class Defaults
    {
        public const string RedisHost = "localhost";
        public const int RedisPort = 6379;
        public const string DefaultConnection = "DefaultConnection";
        public const int DefaultPageSize = 15;
    }
}

