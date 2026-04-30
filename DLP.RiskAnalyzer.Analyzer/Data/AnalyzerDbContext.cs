using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Data;

public class AnalyzerDbContext : DbContext
{
    public AnalyzerDbContext(DbContextOptions<AnalyzerDbContext> options) : base(options)
    {
    }

    public DbSet<Incident> Incidents { get; set; }
    public DbSet<DailySummary> DailySummaries { get; set; }
    public DbSet<UserRiskTrend> UserRiskTrends { get; set; }
    public DbSet<DepartmentSummary> DepartmentSummaries { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<AIBehavioralAnalysis> AIBehavioralAnalyses { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<AnomalyDetection> AnomalyDetections { get; set; }
    public DbSet<NdaDomain> NdaDomains { get; set; }
    public DbSet<UserDailyRiskScore> UserDailyRiskScores { get; set; }
    public DbSet<DomainFeatureDefinition> DomainFeatureDefinitions { get; set; }
    public DbSet<DomainFeatureValue> DomainFeatureValues { get; set; }
    public DbSet<AzureAIExplanation> AzureAIExplanations { get; set; }
    public DbSet<ReleasedIncident> ReleasedIncidents { get; set; }
    public DbSet<MercekIncident> MercekIncidents { get; set; }
    public DbSet<PolicyRuleException> PolicyRuleExceptions { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<PermanentExceptionEntry> PermanentExceptions { get; set; }
    public DbSet<ExceptionRemovalEntry> ExceptionRemovals { get; set; }
    public DbSet<UserActivityLog> UserActivityLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Incident entity
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("incidents");
            entity.HasKey(e => new { e.Id, e.Timestamp });
            
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserEmail).HasColumnName("user_email").IsRequired();
            entity.Property(e => e.Department).HasColumnName("department");
            entity.Property(e => e.Severity).HasColumnName("severity").IsRequired();
            entity.Property(e => e.DataType).HasColumnName("data_type");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").IsRequired();
            entity.Property(e => e.Policy).HasColumnName("policy");
            entity.Property(e => e.Channel).HasColumnName("channel");
            entity.Property(e => e.RiskScore).HasColumnName("risk_score");
            entity.Property(e => e.RepeatCount).HasColumnName("repeat_count").HasDefaultValue(0);
            entity.Property(e => e.DataSensitivity).HasColumnName("data_sensitivity").HasDefaultValue(0);
            
            // New extended fields
            entity.Property(e => e.Action).HasColumnName("action");
            entity.Property(e => e.Destination).HasColumnName("destination");
            entity.Property(e => e.FileName).HasColumnName("file_name");
            entity.Property(e => e.LoginName).HasColumnName("login_name");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.Team).HasColumnName("team");
            entity.Property(e => e.EmailAddress).HasColumnName("email_address");
            entity.Property(e => e.ViolationTriggers).HasColumnName("violation_triggers");
            entity.Property(e => e.MaxMatches).HasColumnName("max_matches").HasDefaultValue(0);
            entity.Property(e => e.RuleName).HasColumnName("rule_name");
            entity.Property(e => e.HostName).HasColumnName("host_name");
            
            // Remediation fields
            entity.Property(e => e.IsRemediated).HasColumnName("is_remediated").HasDefaultValue(false);
            entity.Property(e => e.RemediatedAt).HasColumnName("remediated_at");
            entity.Property(e => e.RemediatedBy).HasColumnName("remediated_by");
            entity.Property(e => e.RemediationAction).HasColumnName("remediation_action");
            entity.Property(e => e.RemediationNotes).HasColumnName("remediation_notes");

            entity.HasIndex(e => e.UserEmail);
            entity.HasIndex(e => e.Department);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.RiskScore);
            entity.HasIndex(e => e.Action);  // Index for action-based queries
            entity.HasIndex(e => e.IsRemediated);  // Index for remediation queries
        });

        // Configure DailySummary
        modelBuilder.Entity<DailySummary>(entity =>
        {
            entity.ToTable("daily_summaries");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Date).HasColumnName("date").IsRequired();
            entity.Property(e => e.TotalIncidents).HasColumnName("total_incidents");
            entity.Property(e => e.HighRiskCount).HasColumnName("high_risk_count");
            entity.Property(e => e.AvgRiskScore).HasColumnName("avg_risk_score");
            entity.Property(e => e.UniqueUsers).HasColumnName("unique_users");
            entity.Property(e => e.DepartmentsAffected).HasColumnName("departments_affected");
        });

        // Configure UserRiskTrend
        modelBuilder.Entity<UserRiskTrend>(entity =>
        {
            entity.ToTable("user_risk_trends");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.UserEmail).HasColumnName("user_email").IsRequired();
            entity.Property(e => e.Date).HasColumnName("date").IsRequired();
            entity.Property(e => e.TotalIncidents).HasColumnName("total_incidents");
            entity.Property(e => e.RiskScore).HasColumnName("risk_score");
            entity.Property(e => e.TrendDirection).HasColumnName("trend_direction");
        });

        // Configure DepartmentSummary
        modelBuilder.Entity<DepartmentSummary>(entity =>
        {
            entity.ToTable("department_summaries");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Department).HasColumnName("department").IsRequired();
            entity.Property(e => e.TotalIncidents).HasColumnName("total_incidents");
            entity.Property(e => e.HighRiskCount).HasColumnName("high_risk_count");
            entity.Property(e => e.AvgRiskScore).HasColumnName("avg_risk_score");
            entity.Property(e => e.UniqueUsers).HasColumnName("unique_users");
            entity.Property(e => e.Date).HasColumnName("date");
        });

        // Configure SystemSetting
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("system_settings");
            entity.HasKey(e => e.Key);
            
            entity.Property(e => e.Key).HasColumnName("key").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasColumnName("value").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Configure AIBehavioralAnalysis
        modelBuilder.Entity<AIBehavioralAnalysis>(entity =>
        {
            entity.ToTable("ai_behavioral_analyses");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired().HasMaxLength(255);
            entity.Property(e => e.AnalysisDate).HasColumnName("analysis_date").IsRequired();
            entity.Property(e => e.RiskScore).HasColumnName("risk_score").IsRequired();
            entity.Property(e => e.AnomalyLevel).HasColumnName("anomaly_level").IsRequired().HasMaxLength(20);
            entity.Property(e => e.AIExplanation).HasColumnName("ai_explanation").IsRequired();
            entity.Property(e => e.AIRecommendation).HasColumnName("ai_recommendation").IsRequired();
            entity.Property(e => e.ReferenceIncidentIds).HasColumnName("reference_incident_ids");
            entity.Property(e => e.AnalysisMetadata).HasColumnName("analysis_metadata");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.AnalysisDate);
            entity.HasIndex(e => e.AnomalyLevel);
        });

        // Configure AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").IsRequired();
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserName).HasColumnName("user_name").IsRequired().HasMaxLength(100);
            entity.Property(e => e.UserRole).HasColumnName("user_role").HasMaxLength(50);
            entity.Property(e => e.Action).HasColumnName("action").IsRequired().HasMaxLength(200);
            entity.Property(e => e.Resource).HasColumnName("resource").HasMaxLength(255);
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.Success).HasColumnName("success").IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.StatusCode).HasColumnName("status_code");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.UserName);
            entity.HasIndex(e => new { e.Timestamp, e.EventType });
        });

        // Configure AnomalyDetection
        modelBuilder.Entity<AnomalyDetection>(entity =>
        {
            entity.ToTable("anomaly_detections");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserEmail).HasColumnName("user_email").IsRequired().HasMaxLength(255);
            entity.Property(e => e.MetricType).HasColumnName("metric_type").IsRequired().HasMaxLength(50);
            entity.Property(e => e.CurrentValue).HasColumnName("current_value").IsRequired();
            entity.Property(e => e.BaselineMean).HasColumnName("baseline_mean").IsRequired();
            entity.Property(e => e.BaselineStdDev).HasColumnName("baseline_std_dev").IsRequired();
            entity.Property(e => e.AnomalyScore).HasColumnName("anomaly_score").IsRequired();
            entity.Property(e => e.Severity).HasColumnName("severity").IsRequired().HasMaxLength(20);
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").IsRequired();

            entity.HasIndex(e => e.UserEmail);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => new { e.UserEmail, e.Timestamp });
        });

        // Configure NdaDomain
        modelBuilder.Entity<NdaDomain>(entity =>
        {
            entity.ToTable("nda_domains");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Domain).HasColumnName("domain").IsRequired().HasMaxLength(255);
            entity.Property(e => e.HasNda).HasColumnName("has_nda").HasDefaultValue(false);
            entity.Property(e => e.IsUnknown).HasColumnName("is_unknown").HasDefaultValue(false);
            entity.Property(e => e.IsPersonal).HasColumnName("is_personal").HasDefaultValue(false);
            
            // New feature columns
            entity.Property(e => e.IstirakDomain).HasColumnName("istirak_domain").HasDefaultValue(false);
            entity.Property(e => e.Egitim).HasColumnName("egitim").HasDefaultValue(false);
            entity.Property(e => e.Noter).HasColumnName("noter").HasDefaultValue(false);
            entity.Property(e => e.Hukuk).HasColumnName("hukuk").HasDefaultValue(false);
            entity.Property(e => e.Denetim).HasColumnName("denetim").HasDefaultValue(false);
            entity.Property(e => e.Banka).HasColumnName("banka").HasDefaultValue(false);
            
            // NDA tracking fields
            entity.Property(e => e.NdaFilePath).HasColumnName("nda_file_path").HasMaxLength(1000);
            entity.Property(e => e.NdaUpdatedBy).HasColumnName("nda_updated_by").HasMaxLength(100);
            entity.Property(e => e.NdaUpdatedAt).HasColumnName("nda_updated_at");
            
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Domain).IsUnique();
            entity.HasIndex(e => e.HasNda);
            entity.HasIndex(e => e.IsUnknown);
            entity.HasIndex(e => e.IsPersonal);
        });

        // Configure DomainFeatureDefinition
        modelBuilder.Entity<DomainFeatureDefinition>(entity =>
        {
            entity.ToTable("domain_feature_definitions");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.KeyName).HasColumnName("key_name").IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasColumnName("display_name").IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.KeyName).IsUnique();
        });

        // Configure DomainFeatureValue
        modelBuilder.Entity<DomainFeatureValue>(entity =>
        {
            entity.ToTable("domain_feature_values");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.DomainId).HasColumnName("domain_id");
            entity.Property(e => e.FeatureId).HasColumnName("feature_id");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.DomainId, e.FeatureId }).IsUnique();
            
            entity.HasOne(d => d.Domain)
                .WithMany()
                .HasForeignKey(d => d.DomainId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Feature)
                .WithMany(p => p.Values)
                .HasForeignKey(d => d.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UserDailyRiskScore
        modelBuilder.Entity<UserDailyRiskScore>(entity =>
        {
            entity.ToTable("user_daily_risk_scores");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserEmail).HasColumnName("user_email").IsRequired().HasMaxLength(255);
            entity.Property(e => e.Date).HasColumnName("date").IsRequired();
            entity.Property(e => e.DailyRiskScore).HasColumnName("daily_risk_score").HasDefaultValue(0);
            entity.Property(e => e.IncidentCount).HasColumnName("incident_count").HasDefaultValue(0);
            entity.Property(e => e.MaxRiskScore).HasColumnName("max_risk_score").HasDefaultValue(0);
            entity.Property(e => e.AvgRiskScore).HasColumnName("avg_risk_score").HasDefaultValue(0);
            entity.Property(e => e.Team).HasColumnName("team");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.EmailAddress).HasColumnName("email_address").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.UserEmail);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.UserEmail, e.Date }).IsUnique();
        });

        // Configure AzureAIExplanation
        modelBuilder.Entity<AzureAIExplanation>(entity =>
        {
            entity.ToTable("ai_explanations");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.UserEmail).HasColumnName("user_email").IsRequired().HasMaxLength(255);
            entity.Property(e => e.RiskLevel).HasColumnName("risk_level").HasMaxLength(50);
            entity.Property(e => e.RiskScore).HasColumnName("risk_score");
            entity.Property(e => e.AnomalyDetectedText).HasColumnName("anomaly_detected");
            entity.Property(e => e.Explanation).HasColumnName("explanation");
            entity.Property(e => e.RecommendedAction).HasColumnName("recommended_action");
            entity.Property(e => e.TimestampArray).HasColumnName("timestamp");

            entity.HasIndex(e => e.UserEmail);
            entity.HasIndex(e => e.IncidentId);
            entity.HasIndex(e => e.RiskLevel);
            
            // Ignore computed properties
            entity.Ignore(e => e.AnomalyDetected);
            entity.Ignore(e => e.Timestamp);
        });

        // Configure ReleasedIncident
        modelBuilder.Entity<ReleasedIncident>(entity =>
        {
            entity.ToTable("released_incidents");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.IncidentId).HasColumnName("incident_id").IsRequired();
            entity.Property(e => e.IncidentTimestamp).HasColumnName("incident_timestamp").IsRequired();
            entity.Property(e => e.Action).HasColumnName("action").IsRequired().HasMaxLength(50);
            entity.Property(e => e.TaskName).HasColumnName("task_name").IsRequired().HasMaxLength(255);
            entity.Property(e => e.AdminName).HasColumnName("admin_name").HasMaxLength(100);
            entity.Property(e => e.Comments).HasColumnName("comments");
            entity.Property(e => e.UpdateTime).HasColumnName("update_time");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.IncidentId);
            entity.HasIndex(e => e.AdminName);
            entity.HasIndex(e => e.UpdateTime);
            entity.HasIndex(e => e.IncidentTimestamp);
            entity.HasIndex(e => new { e.IncidentId, e.UpdateTime }).IsUnique();
        });

        // Configure MercekIncident
        modelBuilder.Entity<MercekIncident>(entity =>
        {
            entity.ToTable("merceks");
            entity.HasKey(e => e.IncidentId);
            
            entity.Property(e => e.IncidentId).HasColumnName("incidentid");
            entity.Property(e => e.StatusId).HasColumnName("statusid").HasMaxLength(50);
            entity.Property(e => e.FlowStatusId).HasColumnName("flowstatusid").HasMaxLength(50);
            entity.Property(e => e.AssignmentGroupId).HasColumnName("assignmentgroupid");
            entity.Property(e => e.SummaryDescription).HasColumnName("summarydescription").HasMaxLength(500);
            entity.Property(e => e.IncidentDescription).HasColumnName("incidentdescription");
            entity.Property(e => e.ImpactId).HasColumnName("impactid").HasMaxLength(50);
            entity.Property(e => e.PriorityId).HasColumnName("priorityid").HasMaxLength(50);
            entity.Property(e => e.CategoryId).HasColumnName("categoryid");
            entity.Property(e => e.AssignedUserCode).HasColumnName("assignedusercode").HasMaxLength(100);
            entity.Property(e => e.OpenDate).HasColumnName("opendate");
            entity.Property(e => e.CloseDate).HasColumnName("closedate");
            entity.Property(e => e.StartDate).HasColumnName("startdate");
            entity.Property(e => e.SolutionDescription).HasColumnName("solutiondescription");
            entity.Property(e => e.RequestTypeId).HasColumnName("requesttypeid").HasMaxLength(50);
            entity.Property(e => e.CallTypeId).HasColumnName("calltypeid").HasMaxLength(50);
            entity.Property(e => e.SolutionMethod).HasColumnName("solutionmethod").HasMaxLength(200);
            entity.Property(e => e.UserName).HasColumnName("username").HasMaxLength(200);
            entity.Property(e => e.SystemDate).HasColumnName("systemdate");
            entity.Property(e => e.DefinitionCategoryId).HasColumnName("definitioncategoryid");
            entity.Property(e => e.DefinitionCategoryPath).HasColumnName("definitioncategorypath").HasMaxLength(500);

            entity.HasIndex(e => e.OpenDate);
            entity.HasIndex(e => e.CloseDate);
            entity.HasIndex(e => e.SystemDate);
            entity.HasIndex(e => e.UserName);
            entity.HasIndex(e => e.AssignedUserCode);
            entity.HasIndex(e => e.StatusId);
        });

        // Configure PolicyRuleException
        modelBuilder.Entity<PolicyRuleException>(entity =>
        {
            entity.ToTable("policy_rule_exceptions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.PolicyName).HasColumnName("policy_name").IsRequired().HasMaxLength(500);
            entity.Property(e => e.RuleName).HasColumnName("rule_name").IsRequired().HasMaxLength(500);
            entity.Property(e => e.ExceptionName).HasColumnName("exception_name").IsRequired().HasMaxLength(500);
            entity.Property(e => e.SyncedAt).HasColumnName("synced_at");

            entity.HasIndex(e => e.PolicyName);
            entity.HasIndex(e => e.ExceptionName);
            entity.HasIndex(e => new { e.PolicyName, e.ExceptionName });
        });

        // Configure UserEntity
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Username).HasColumnName("username").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.Role).HasColumnName("role").IsRequired().HasMaxLength(20).HasDefaultValue("standard");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.PasswordSalt).HasColumnName("password_salt").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Configure PermanentExceptionEntry
        modelBuilder.Entity<PermanentExceptionEntry>(entity =>
        {
            entity.ToTable("permanent_exceptions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ExceptionName).HasColumnName("exception_name").IsRequired().HasMaxLength(500);
            entity.Property(e => e.ExceptionDomain).HasColumnName("exception_domain").HasMaxLength(1000);
            entity.Property(e => e.Team).HasColumnName("team").HasMaxLength(500);
            entity.Property(e => e.Policies).HasColumnName("policies").HasMaxLength(2000);
            entity.Property(e => e.Rules).HasColumnName("rules").HasMaxLength(2000);
            entity.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(200);
            entity.Property(e => e.Duration).HasColumnName("duration").HasMaxLength(100);
            entity.Property(e => e.ActionDate).HasColumnName("action_date");
            entity.Property(e => e.ChangeNo).HasColumnName("change_no").HasMaxLength(200);
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.ExceptionName);
            entity.HasIndex(e => e.Team);
            entity.HasIndex(e => e.ActionDate);
        });

        // Configure ExceptionRemovalEntry
        modelBuilder.Entity<ExceptionRemovalEntry>(entity =>
        {
            entity.ToTable("exception_removals");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Team).HasColumnName("team").HasMaxLength(500);
            entity.Property(e => e.Rule).HasColumnName("rule").HasMaxLength(500);
            entity.Property(e => e.ExceptionName).HasColumnName("exception_name").IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.UsageCount).HasColumnName("usage_count").HasDefaultValue(0);
            entity.Property(e => e.RemovalReason).HasColumnName("removal_reason").HasMaxLength(1000);
            entity.Property(e => e.ActionDate).HasColumnName("action_date");
            entity.Property(e => e.ChangeNo).HasColumnName("change_no").HasMaxLength(200);
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.ExceptionName);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ActionDate);
        });

        // Configure UserActivityLog
        modelBuilder.Entity<UserActivityLog>(entity =>
        {
            entity.ToTable("user_activity_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").IsRequired();
            entity.Property(e => e.UserName).HasColumnName("user_name").IsRequired().HasMaxLength(100);
            entity.Property(e => e.AuthSource).HasColumnName("auth_source").HasMaxLength(20).HasDefaultValue("Local");
            entity.Property(e => e.ActivityType).HasColumnName("activity_type").IsRequired().HasMaxLength(50);
            entity.Property(e => e.PagePath).HasColumnName("page_path").HasMaxLength(500);
            entity.Property(e => e.PageTitle).HasColumnName("page_title").HasMaxLength(200);
            entity.Property(e => e.ActionDetail).HasColumnName("action_detail").HasMaxLength(2000);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.SessionDurationSeconds).HasColumnName("session_duration_seconds");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserName);
            entity.HasIndex(e => e.ActivityType);
            entity.HasIndex(e => e.AuthSource);
            entity.HasIndex(e => new { e.UserName, e.Timestamp });
        });
    }
}