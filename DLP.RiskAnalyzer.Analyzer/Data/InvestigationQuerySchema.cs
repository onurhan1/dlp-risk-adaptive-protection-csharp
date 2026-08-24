using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Data;

public static class InvestigationQuerySchema
{
    private const string Ddl = @"
        CREATE SCHEMA IF NOT EXISTS dlp;

        CREATE TABLE IF NOT EXISTS dlp.investigation_queries (
            id SERIAL PRIMARY KEY,
            user_code VARCHAR(120) NOT NULL DEFAULT '',
            full_name VARCHAR(255) NOT NULL DEFAULT '',
            mail_address VARCHAR(255) NOT NULL DEFAULT '',
            subject VARCHAR(500) NOT NULL DEFAULT '',
            query_date TIMESTAMP,
            response_status VARCHAR(255) NOT NULL DEFAULT '',
            action TEXT NOT NULL DEFAULT '',
            query_status VARCHAR(50) NOT NULL DEFAULT 'bekliyor',
            source VARCHAR(80),
            team VARCHAR(255),
            notes TEXT,
            playbook_mail_log_id INTEGER,
            extra_json TEXT NOT NULL DEFAULT (chr(123) || chr(125)),
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_by VARCHAR(120),
            updated_by VARCHAR(120)
        );

        CREATE INDEX IF NOT EXISTS ix_investigation_queries_mail
            ON dlp.investigation_queries (mail_address);
        CREATE INDEX IF NOT EXISTS ix_investigation_queries_query_date
            ON dlp.investigation_queries (query_date DESC);
        CREATE INDEX IF NOT EXISTS ix_investigation_queries_status
            ON dlp.investigation_queries (query_status);
        ALTER TABLE dlp.investigation_queries
            ADD COLUMN IF NOT EXISTS playbook_mail_log_id INTEGER;
        ALTER TABLE dlp.investigation_queries
            ADD COLUMN IF NOT EXISTS user_code VARCHAR(120) NOT NULL DEFAULT '';
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS correlation_code VARCHAR(80);
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS first_sent_at TIMESTAMP;
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS reply_received_at TIMESTAMP;
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS reply_message_id VARCHAR(500);
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS reply_preview TEXT;
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS review_note TEXT;
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS reminder_sent_at TIMESTAMP;
        ALTER TABLE dlp.investigation_queries ADD COLUMN IF NOT EXISTS reminder_count INTEGER NOT NULL DEFAULT 0;
        CREATE INDEX IF NOT EXISTS ix_investigation_queries_user_code
            ON dlp.investigation_queries (user_code);
        CREATE INDEX IF NOT EXISTS ix_investigation_queries_mail_log
            ON dlp.investigation_queries (playbook_mail_log_id);
        CREATE INDEX IF NOT EXISTS ix_investigation_queries_correlation
            ON dlp.investigation_queries (correlation_code);

        CREATE TABLE IF NOT EXISTS dlp.investigation_inbound_mails (
            id SERIAL PRIMARY KEY,
            message_key VARCHAR(500) NOT NULL UNIQUE,
            rfc_message_id VARCHAR(500),
            from_email VARCHAR(255) NOT NULL DEFAULT '',
            subject VARCHAR(500) NOT NULL DEFAULT '',
            received_at TIMESTAMP,
            body_preview TEXT,
            investigation_query_id INTEGER,
            processing_result VARCHAR(80) NOT NULL DEFAULT '',
            processed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS ix_investigation_inbound_query
            ON dlp.investigation_inbound_mails (investigation_query_id);
    ";

    public static async Task EnsureAsync(AnalyzerDbContext context, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(Ddl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure investigation query table (may already exist)");
        }
    }
}
