using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Data;

/// <summary>
/// Runtime provisioning for the playbook tables, mirroring the approach already used for
/// mail_templates: the tables are created on demand with CREATE TABLE IF NOT EXISTS and are
/// excluded from EF migrations, so the feature works on servers where migrations have not been
/// applied. Both the controller and the scheduler background service call this, which is why it
/// lives here instead of inside a controller.
/// </summary>
public static class PlaybookSchema
{
    private const string Ddl = @"
        CREATE SCHEMA IF NOT EXISTS dlp;

        CREATE TABLE IF NOT EXISTS dlp.playbooks (
            id SERIAL PRIMARY KEY,
            name VARCHAR(255) NOT NULL,
            description VARCHAR(1000),
            graph_json TEXT NOT NULL,
            enabled BOOLEAN NOT NULL DEFAULT FALSE,
            auto_send BOOLEAN NOT NULL DEFAULT FALSE,
            schedule_cron VARCHAR(120),
            last_run_at TIMESTAMP,
            next_run_at TIMESTAMP,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS ix_playbooks_enabled_next_run
            ON dlp.playbooks (enabled, next_run_at);

        CREATE TABLE IF NOT EXISTS dlp.playbook_runs (
            id SERIAL PRIMARY KEY,
            playbook_id INTEGER NOT NULL,
            started_at TIMESTAMP NOT NULL,
            finished_at TIMESTAMP,
            status VARCHAR(30) NOT NULL,
            trigger_type VARCHAR(20) NOT NULL,
            dry_run BOOLEAN NOT NULL DEFAULT TRUE,
            node_log_json TEXT,
            mails_sent INTEGER NOT NULL DEFAULT 0,
            mails_pending INTEGER NOT NULL DEFAULT 0,
            mails_failed INTEGER NOT NULL DEFAULT 0,
            mails_skipped INTEGER NOT NULL DEFAULT 0,
            error_message TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_playbook_runs_playbook_started
            ON dlp.playbook_runs (playbook_id, started_at DESC);
        CREATE INDEX IF NOT EXISTS ix_playbook_runs_status
            ON dlp.playbook_runs (status);

        CREATE TABLE IF NOT EXISTS dlp.playbook_mail_log (
            id SERIAL PRIMARY KEY,
            run_id INTEGER NOT NULL,
            playbook_id INTEGER NOT NULL,
            node_id VARCHAR(64) NOT NULL,
            user_email VARCHAR(255) NOT NULL,
            full_name VARCHAR(255),
            team VARCHAR(255),
            to_email VARCHAR(255) NOT NULL,
            cc_email VARCHAR(255),
            subject VARCHAR(500) NOT NULL,
            body_html TEXT NOT NULL,
            source_criterion VARCHAR(60),
            trigger_count INTEGER NOT NULL DEFAULT 0,
            status VARCHAR(20) NOT NULL,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            sent_at TIMESTAMP,
            error_message TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_playbook_mail_log_run
            ON dlp.playbook_mail_log (run_id);
        CREATE INDEX IF NOT EXISTS ix_playbook_mail_log_playbook_created
            ON dlp.playbook_mail_log (playbook_id, created_at DESC);
        CREATE INDEX IF NOT EXISTS ix_playbook_mail_log_status
            ON dlp.playbook_mail_log (status);
    ";

    /// <summary>
    /// Creates the playbook tables when missing. Failures are logged and swallowed — the caller
    /// will surface a real error if the subsequent query cannot run.
    /// </summary>
    public static async Task EnsureAsync(AnalyzerDbContext context, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(Ddl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure playbook tables (may already exist)");
        }
    }
}
