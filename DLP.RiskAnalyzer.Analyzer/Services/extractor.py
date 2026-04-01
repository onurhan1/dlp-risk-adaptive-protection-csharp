import os
import re

path = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Analyzer\Services\BehaviorEngineService.cs"

with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# Models
models_pattern = re.compile(r"    private class BehaviorMetrics.*?    }\n\n    #endregion", re.DOTALL)
models_match = models_pattern.search(text)
if models_match:
    models_code = models_match.group(0).replace("private class", "public class")
    # Write BehaviorModels.cs
    with open(os.path.join(os.path.dirname(path), '..', 'Models', 'BehaviorModels.cs'), 'w', encoding='utf-8') as mf:
        mf.write('using System.Collections.Generic;\n\nnamespace DLP.RiskAnalyzer.Analyzer.Models;\n\n')
        mf.write(models_code.replace("    #endregion", ""))

# AI Explanation Service
ai_pattern = re.compile(r"    private async Task<\(string Explanation.*?    }\n\n    #endregion", re.DOTALL)
ai_match = ai_pattern.search(text)

# Behavior Metrics Calculator
calc_pattern = re.compile(r"    private BehaviorMetrics CalculateEnhancedMetrics.*?    #endregion", re.DOTALL)
calc_match = calc_pattern.search(text)

det_pattern = re.compile(r"    private string DetermineAnomalyLevel\(int riskScore\).*?    }\n", re.DOTALL)
det_match = det_pattern.search(text)

if ai_match and calc_match and det_match:
    ai_code = ai_match.group(0).replace("private async", "public async").replace("private string", "public string").replace("    #endregion", "")
    calc_code = calc_match.group(0).replace("private BehaviorMetrics", "public BehaviorMetrics").replace("private Dictionary", "public Dictionary").replace("private double", "public double").replace("private int", "public int").replace("private List", "public List")
    det_code = det_match.group(0).replace("private string Determine", "public string Determine")
    
    with open(os.path.join(os.path.dirname(path), 'BehaviorAIExplanationService.cs'), 'w', encoding='utf-8') as aif:
        aif.write("""using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IBehaviorAIExplanationService
{
    Task<(string Explanation, string Recommendation)> GenerateAIAnalysisAsync(string entityType, string entityId, Dictionary<string, object> analysisData, AnomalyResults anomalyResults);
    string GenerateExplanation(string entityType, string entityId, BehaviorMetrics currentMetrics, BehaviorMetrics baselineMetrics, AnomalyResults anomalyResults);
    string GenerateRecommendation(AnomalyResults anomalyResults, string entityType);
}

public class BehaviorAIExplanationService : IBehaviorAIExplanationService
{
    private readonly IAIAnalysisRepository _aiAnalysisRepository;
    private readonly ILogger<BehaviorAIExplanationService> _logger;
    private readonly OpenAIService? _openAIService;
    private readonly AzureOpenAIService? _azureOpenAIService;
    private readonly CopilotService? _copilotService;

    public BehaviorAIExplanationService(
        IAIAnalysisRepository aiAnalysisRepository,
        ILogger<BehaviorAIExplanationService> logger,
        IServiceProvider serviceProvider)
    {
        _aiAnalysisRepository = aiAnalysisRepository;
        _logger = logger;
        try { _openAIService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<OpenAIService>(serviceProvider); } catch {}
        try { _azureOpenAIService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<AzureOpenAIService>(serviceProvider); } catch {}
        try { _copilotService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<CopilotService>(serviceProvider); } catch {}
    }

""")
        aif.write(ai_code)
        aif.write("\n}\n")

    with open(os.path.join(os.path.dirname(path), 'BehaviorMetricsCalculator.cs'), 'w', encoding='utf-8') as cf:
        cf.write("""using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IBehaviorMetricsCalculator
{
    BehaviorMetrics CalculateEnhancedMetrics(List<Incident> incidents);
    Dictionary<string, double> CalculateAllZScores(BehaviorMetrics current, BehaviorMetrics baseline, List<Incident> currentIncidents, List<Incident> baselineIncidents);
    int CalculateEnhancedRiskScore(Dictionary<string, double> zScores);
    string DetermineAnomalyLevel(int riskScore);
    double CalculateThreatProfileMultiplier(List<Incident> incidents);
    List<TrendDataPoint> GenerateWeeklyTrends(List<Incident> incidents, int lookbackDays);
    List<TrendDataPoint> GenerateMonthlyTrends(List<Incident> incidents);
    List<DestinationPattern> GetDestinationPatterns(List<Incident> currentIncidents, List<Incident> baselineIncidents);
    int GetEffectiveMaxMatches(Incident incident);
}

public class BehaviorMetricsCalculator : IBehaviorMetricsCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

""")
        cf.write(calc_code)
        cf.write("\n")
        cf.write(det_code)
        cf.write("\n}\n")

    # Now strip them from the original file!
    new_text = text.replace(models_match.group(0), "")
    new_text = new_text.replace(ai_match.group(0), "")
    new_text = new_text.replace(calc_match.group(0), "")
    new_text = new_text.replace(det_match.group(0), "")
    
    with open(path, 'w', encoding='utf-8') as f:
        f.write(new_text)
    print("Extraction successful.")
else:
    print("Could not find patterns.")
