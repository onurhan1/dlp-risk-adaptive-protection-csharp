import re
import os

path = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Analyzer\Services\BehaviorEngineService.cs"

with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# Replace fields and constructor
old_constructor = r"""    private readonly IIncidentRepository _incidentRepository;
    private readonly IAIAnalysisRepository _aiAnalysisRepository;
    private readonly ILogger<BehaviorEngineService> _logger;
    private readonly IDataProtector _protector;
    private readonly OpenAIService\? _openAIService;
    private readonly AzureOpenAIService\? _azureOpenAIService;
    private readonly CopilotService\? _copilotService;

    private const string OpenAIKeyKey = "ai_openai_api_key_protected";
    private const string CopilotKeyKey = "ai_copilot_api_key_protected";
    private const string AzureKeyKey = "ai_azure_openai_key_protected";
    private const string AzureEndpointKey = "ai_azure_openai_endpoint";
    private const string ModelProviderKey = "ai_model_provider";
    private const string ModelNameKey = "ai_model_name";
    private const string TemperatureKey = "ai_temperature";
    private const string MaxTokensKey = "ai_max_tokens";

    public BehaviorEngineService\(
        IIncidentRepository incidentRepository,
        IAIAnalysisRepository aiAnalysisRepository,
        ILogger<BehaviorEngineService> logger,
        IDataProtectionProvider dataProtectionProvider,
        IServiceProvider serviceProvider\)
    \{
        _incidentRepository = incidentRepository;
        _aiAnalysisRepository = aiAnalysisRepository;
        _logger = logger;
        _protector = dataProtectionProvider\.CreateProtector\("AI\.SettingsProtector"\);
        
        // Get AI services if available \(optional dependencies\)
        try
        \{
            _openAIService = serviceProvider\.GetService<OpenAIService>\(\);
        \}
        catch
        \{
            _openAIService = null;
        \}

        try
        \{
            _azureOpenAIService = serviceProvider\.GetService<AzureOpenAIService>\(\);
        \}
        catch
        \{
            _azureOpenAIService = null;
        \}

        try
        \{
            _copilotService = serviceProvider\.GetService<CopilotService>\(\);
        \}
        catch
        \{
            _copilotService = null;
        \}
    \}"""

new_constructor = """    private readonly IIncidentRepository _incidentRepository;
    private readonly IAIAnalysisRepository _aiAnalysisRepository;
    private readonly ILogger<BehaviorEngineService> _logger;
    private readonly IBehaviorMetricsCalculator _metricsCalculator;
    private readonly IBehaviorAIExplanationService _aiExplanationService;

    public BehaviorEngineService(
        IIncidentRepository incidentRepository,
        IAIAnalysisRepository aiAnalysisRepository,
        ILogger<BehaviorEngineService> logger,
        IBehaviorMetricsCalculator metricsCalculator,
        IBehaviorAIExplanationService aiExplanationService)
    {
        _incidentRepository = incidentRepository;
        _aiAnalysisRepository = aiAnalysisRepository;
        _logger = logger;
        _metricsCalculator = metricsCalculator;
        _aiExplanationService = aiExplanationService;
    }"""

text = re.sub(old_constructor, new_constructor, text)

# Replace remaining fields
replacements = {
    r"CalculateEnhancedMetrics\(": "_metricsCalculator.CalculateEnhancedMetrics(",
    r"CalculateAllZScores\(": "_metricsCalculator.CalculateAllZScores(",
    r"CalculateEnhancedRiskScore\(": "_metricsCalculator.CalculateEnhancedRiskScore(",
    r"CalculateThreatProfileMultiplier\(": "_metricsCalculator.CalculateThreatProfileMultiplier(",
    r"DetermineAnomalyLevel\(": "_metricsCalculator.DetermineAnomalyLevel(",
    r"GenerateWeeklyTrends\(": "_metricsCalculator.GenerateWeeklyTrends(",
    r"GenerateMonthlyTrends\(": "_metricsCalculator.GenerateMonthlyTrends(",
    r"GetDestinationPatterns\(": "_metricsCalculator.GetDestinationPatterns(",
    r"GetEffectiveMaxMatches\(": "_metricsCalculator.GetEffectiveMaxMatches(",
    r"GenerateAIAnalysisAsync\(": "_aiExplanationService.GenerateAIAnalysisAsync(",
    r"GenerateExplanation\(": "_aiExplanationService.GenerateExplanation(",
    r"GenerateRecommendation\(": "_aiExplanationService.GenerateRecommendation("
}

for k, v in replacements.items():
    # Only replace if not preceded by public or private declaration
    text = re.sub(r"(?<!public )(?<!private )(?<!class )(?<!interface )" + k, v, text)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print("Usages fixed.")
