using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLP.RiskAnalyzer.Shared.Models
{
    // Katman 1: Policy
    [Table("pi_policies")]
    public class PIPolicy
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("policy_name")]
        public string PolicyName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<PIRule> Rules { get; set; } = new List<PIRule>();
    }

    // Katman 2: Rule
    [Table("pi_rules")]
    public class PIRule
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("policy_id")]
        public int PolicyId { get; set; }

        [Required]
        [Column("rule_name")]
        public string RuleName { get; set; }

        [Column("parts_count_type")]
        public string PartsCountType { get; set; }

        [Column("condition_relation_type")]
        public string ConditionRelationType { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("PolicyId")]
        public PIPolicy Policy { get; set; }

        public ICollection<PIRuleClassifier> Classifiers { get; set; } = new List<PIRuleClassifier>();
        public ICollection<PIRuleSeverityAction> SeverityActions { get; set; } = new List<PIRuleSeverityAction>();
        public ICollection<PIRuleSource> Sources { get; set; } = new List<PIRuleSource>();
        public ICollection<PIRuleDestination> Destinations { get; set; } = new List<PIRuleDestination>();
        public ICollection<PIException> Exceptions { get; set; } = new List<PIException>();
    }

    [Table("pi_rule_classifiers")]
    public class PIRuleClassifier
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rule_id")]
        public int RuleId { get; set; }

        [Column("classifier_name")]
        public string ClassifierName { get; set; }

        [Column("threshold_type")]
        public string ThresholdType { get; set; }

        [Column("threshold_value_from")]
        public int? ThresholdValueFrom { get; set; }

        [Column("threshold_calculate_type")]
        public string ThresholdCalculateType { get; set; }

        [ForeignKey("RuleId")]
        public PIRule Rule { get; set; }
    }

    [Table("pi_rule_severity_actions")]
    public class PIRuleSeverityAction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rule_id")]
        public int RuleId { get; set; }

        [Column("type")]
        public string Type { get; set; }

        [Column("max_matches")]
        public string MaxMatches { get; set; }

        [Column("selected")]
        public string Selected { get; set; }

        [Column("number_of_matches")]
        public int? NumberOfMatches { get; set; }

        [Column("severity_type")]
        public string SeverityType { get; set; }

        [Column("dup_severity_type")]
        public string DupSeverityType { get; set; }

        [Column("action_plan")]
        public string ActionPlan { get; set; }

        [ForeignKey("RuleId")]
        public PIRule Rule { get; set; }
    }

    [Table("pi_rule_sources")]
    public class PIRuleSource
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rule_id")]
        public int RuleId { get; set; }

        [Column("resource_name")]
        public string ResourceName { get; set; }

        [Column("resource_type")]
        public string ResourceType { get; set; }

        [Column("include")]
        public string Include { get; set; }

        [ForeignKey("RuleId")]
        public PIRule Rule { get; set; }
    }

    [Table("pi_rule_destinations")]
    public class PIRuleDestination
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rule_id")]
        public int RuleId { get; set; }

        [Column("email_monitor_directions")]
        public string EmailMonitorDirections { get; set; }

        [Column("channel_type")]
        public string ChannelType { get; set; }

        [Column("channel_enabled")]
        public string ChannelEnabled { get; set; }

        [ForeignKey("RuleId")]
        public PIRule Rule { get; set; }

        public ICollection<PIRuleChannelResource> ChannelResources { get; set; } = new List<PIRuleChannelResource>();
    }

    [Table("pi_rule_channel_resources")]
    public class PIRuleChannelResource
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("destination_id")]
        public int DestinationId { get; set; }

        [Column("resource_name")]
        public string ResourceName { get; set; }

        [Column("resource_type")]
        public string ResourceType { get; set; }

        [Column("include")]
        public string Include { get; set; }

        [ForeignKey("DestinationId")]
        public PIRuleDestination Destination { get; set; }
    }

    // Katman 3: Exception
    [Table("pi_exceptions")]
    public class PIException
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rule_id")]
        public int RuleId { get; set; }

        [Required]
        [Column("exception_rule_name")]
        public string ExceptionRuleName { get; set; }

        [Column("enabled")]
        public string Enabled { get; set; } = "true";

        [Column("description")]
        public string Description { get; set; }

        [Column("condition_enabled")]
        public string ConditionEnabled { get; set; } = "false";

        [Column("source_enabled")]
        public string SourceEnabled { get; set; } = "false";

        [Column("destination_enabled")]
        public string DestinationEnabled { get; set; } = "false";

        [Column("parts_count_type")]
        public string PartsCountType { get; set; }

        [Column("condition_relation_type")]
        public string ConditionRelationType { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("RuleId")]
        public PIRule Rule { get; set; }

        public ICollection<PIExceptionClassifier> Classifiers { get; set; } = new List<PIExceptionClassifier>();
        public ICollection<PIExceptionSeverityAction> SeverityActions { get; set; } = new List<PIExceptionSeverityAction>();
        public ICollection<PIExceptionSource> Sources { get; set; } = new List<PIExceptionSource>();
        public ICollection<PIExceptionDestination> Destinations { get; set; } = new List<PIExceptionDestination>();
    }

    [Table("pi_exception_classifiers")]
    public class PIExceptionClassifier
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("exception_id")]
        public int ExceptionId { get; set; }

        [Column("classifier_name")]
        public string ClassifierName { get; set; }

        [Column("position")]
        public int? Position { get; set; }

        [Column("threshold_type")]
        public string ThresholdType { get; set; }

        [Column("threshold_value_from")]
        public int? ThresholdValueFrom { get; set; }

        [Column("threshold_calculate_type")]
        public string ThresholdCalculateType { get; set; }

        [Column("analyzed_specific_fields")]
        public string AnalyzedSpecificFields { get; set; }

        [ForeignKey("ExceptionId")]
        public PIException Exception { get; set; }
    }

    [Table("pi_exception_severity_actions")]
    public class PIExceptionSeverityAction
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("exception_id")]
        public int ExceptionId { get; set; }

        [Column("selected")]
        public string Selected { get; set; }

        [Column("number_of_matches")]
        public int? NumberOfMatches { get; set; }

        [Column("severity_type")]
        public string SeverityType { get; set; }

        [Column("dup_severity_type")]
        public string DupSeverityType { get; set; }

        [Column("action_plan")]
        public string ActionPlan { get; set; }

        [ForeignKey("ExceptionId")]
        public PIException Exception { get; set; }
    }

    [Table("pi_exception_sources")]
    public class PIExceptionSource
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("exception_id")]
        public int ExceptionId { get; set; }

        [Column("resource_name")]
        public string ResourceName { get; set; }

        [Column("resource_type")]
        public string ResourceType { get; set; }

        [Column("include")]
        public string Include { get; set; }

        [ForeignKey("ExceptionId")]
        public PIException Exception { get; set; }
    }

    [Table("pi_exception_destinations")]
    public class PIExceptionDestination
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("exception_id")]
        public int ExceptionId { get; set; }

        [Column("email_monitor_directions")]
        public string EmailMonitorDirections { get; set; }

        [Column("channel_type")]
        public string ChannelType { get; set; }

        [Column("channel_enabled")]
        public string ChannelEnabled { get; set; }

        [ForeignKey("ExceptionId")]
        public PIException Exception { get; set; }

        public ICollection<PIExceptionChannelResource> ChannelResources { get; set; } = new List<PIExceptionChannelResource>();
    }

    [Table("pi_exception_channel_resources")]
    public class PIExceptionChannelResource
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("destination_id")]
        public int DestinationId { get; set; }

        [Column("resource_name")]
        public string ResourceName { get; set; }

        [Column("resource_type")]
        public string ResourceType { get; set; }

        [Column("include")]
        public string Include { get; set; }

        [ForeignKey("DestinationId")]
        public PIExceptionDestination Destination { get; set; }
    }
}
