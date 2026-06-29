namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ViolationTriggerDto
{
    [System.Text.Json.Serialization.JsonPropertyName("policy_name")]
    public string? PolicyNameSnake { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("PolicyName")]
    public string? PolicyNamePascal { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("rule_name")]
    public string? RuleNameSnake { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("RuleName")]
    public string? RuleNamePascal { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("severity_level")]
    public int? SeverityLevelSnake { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("SeverityLevel")]
    public int? SeverityLevelPascal { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("match_count")]
    public int? MatchCountSnake { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("MatchCount")]
    public int? MatchCountPascal { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? PolicyName => PolicyNameSnake ?? PolicyNamePascal;
    
    [System.Text.Json.Serialization.JsonIgnore]
    public string? RuleName => RuleNameSnake ?? RuleNamePascal;
    
    [System.Text.Json.Serialization.JsonIgnore]
    public int SeverityLevel => SeverityLevelSnake ?? SeverityLevelPascal ?? 1;
    
    [System.Text.Json.Serialization.JsonIgnore]
    public int MatchCount => MatchCountSnake ?? MatchCountPascal ?? 1;
}
