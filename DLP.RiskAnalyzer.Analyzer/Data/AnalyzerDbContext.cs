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
            entity.Property(e => e.EmailAddress).HasColumnName("email_address");
            entity.Property(e => e.ViolationTriggers).HasColumnName("violation_triggers");

            entity.HasIndex(e => e.UserEmail);
            entity.HasIndex(e => e.Department);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.RiskScore);
            entity.HasIndex(e => e.Action);  // Index for action-based queries
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
    }
}

// SystemSetting entity
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}