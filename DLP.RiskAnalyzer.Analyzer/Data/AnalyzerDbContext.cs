using DLP.RiskAnalyzer.Shared.Models;
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

            entity.HasIndex(e => e.UserEmail);
            entity.HasIndex(e => e.Department);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.RiskScore);
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
    }
}

// SystemSetting entity
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

