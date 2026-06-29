namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IClassificationService
{
    Task<Dictionary<string, object>> GetIncidentClassificationAsync(int incidentId);
    Task<List<Dictionary<string, object>>> GetIncidentFilesAsync(int incidentId);
    Task<Dictionary<string, object>> GetUserClassificationSummaryAsync(string userEmail);
}
