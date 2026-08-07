using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Data;

public static class InvestigationQuerySchema
{
    private const string Ddl = @"
        CREATE SCHEMA IF NOT EXISTS dlp;

        CREATE TABLE IF NOT EXISTS dlp.investigation_queries (
            id SERIAL PRIMARY KEY,
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
        CREATE INDEX IF NOT EXISTS ix_investigation_queries_mail_log
            ON dlp.investigation_queries (playbook_mail_log_id);
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
