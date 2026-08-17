using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Data;

public static class ScheduledJobSchema
{
    private const string Ddl = @"
        CREATE SCHEMA IF NOT EXISTS dlp;

        CREATE TABLE IF NOT EXISTS dlp.scheduled_jobs (
            id SERIAL PRIMARY KEY,
            name VARCHAR(255) NOT NULL,
            description VARCHAR(1000),
            handler_key VARCHAR(100) NOT NULL,
            handler_payload_json TEXT,
            cron_expression VARCHAR(120) NOT NULL,
            enabled BOOLEAN NOT NULL DEFAULT TRUE,
            last_run_at TIMESTAMP,
            next_run_at TIMESTAMP,
            last_status VARCHAR(30) NOT NULL DEFAULT 'never_run',
            last_message TEXT,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS ix_scheduled_jobs_enabled_next_run
            ON dlp.scheduled_jobs (enabled, next_run_at);

        CREATE TABLE IF NOT EXISTS dlp.scheduled_job_runs (
            id SERIAL PRIMARY KEY,
            scheduled_job_id INTEGER NOT NULL,
            started_at TIMESTAMP NOT NULL,
            finished_at TIMESTAMP,
            trigger_type VARCHAR(20) NOT NULL,
            status VARCHAR(30) NOT NULL,
            message TEXT,
            result_json TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_scheduled_job_runs_job_started
            ON dlp.scheduled_job_runs (scheduled_job_id, started_at DESC);
        CREATE INDEX IF NOT EXISTS ix_scheduled_job_runs_status
            ON dlp.scheduled_job_runs (status);
    ";

    public static async Task EnsureAsync(AnalyzerDbContext context, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(Ddl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure scheduled job tables");
        }
    }
}
