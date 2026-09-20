using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Data;

public static class RiskShadowSchema
{
    public static async Task EnsureAsync(AnalyzerDbContext context, CancellationToken ct = default) =>
        await context.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS dlp;
            CREATE TABLE IF NOT EXISTS dlp.risk_shadow_reviews (
                id SERIAL PRIMARY KEY, user_email VARCHAR(255) NOT NULL, verdict VARCHAR(30) NOT NULL,
                reviewer VARCHAR(120) NOT NULL, note TEXT, created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS ix_risk_shadow_reviews_user_created ON dlp.risk_shadow_reviews (user_email, created_at DESC);
            """, ct);
}
