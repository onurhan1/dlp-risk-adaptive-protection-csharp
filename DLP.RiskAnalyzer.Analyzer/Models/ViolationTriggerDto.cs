using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Analyzer.Models;

public class ViolationTriggerDto
{
    [JsonPropertyName("policy_name")]
    public string? PolicyNameSnake { get; set; }
    public string? PolicyName { get; set; }
    
    public string? EffectivePolicyName => PolicyNameSnake ?? PolicyName;

    [JsonPropertyName("rule_name")]
    public string? RuleNameSnake { get; set; }
    public string? RuleName { get; set; }
    
    public string? EffectiveRuleName => RuleNameSnake ?? RuleName;

    public List<ClassifierDto>? Classifiers { get; set; }
}

public class ClassifierDto
{
    [JsonPropertyName("classifier_name")]
    public string? ClassifierNameSnake { get; set; }
    public string? ClassifierName { get; set; }
    
    [JsonPropertyName("number_matches")]
    public int NumberMatchesSnake { get; set; }
    public int NumberMatches { get; set; }
    
    public int EffectiveNumberMatches => NumberMatchesSnake > 0 ? NumberMatchesSnake : NumberMatches;
}
