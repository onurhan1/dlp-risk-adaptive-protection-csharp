using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLP.RiskAnalyzer.Analyzer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AnalyzerDbContext))]
    [Migration("20260601000000_AddIsolationForestScores")]
    public partial class AddIsolationForestScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The context uses "dlp" as its default schema, so the table is created there
            // directly. Installations that still keep the table in "public" are left alone —
            // WebApplicationExtensions.EnsureSchemasAndMoveTablesAsync relocates those, and
            // creating a duplicate in "dlp" here would make that relocation fail.
            migrationBuilder.Sql(@"
                CREATE SCHEMA IF NOT EXISTS dlp;

                DO $$
                BEGIN
                    IF to_regclass('dlp.isolation_forest_scores') IS NULL
                       AND to_regclass('public.isolation_forest_scores') IS NULL THEN

                        CREATE TABLE dlp.isolation_forest_scores (
                            id SERIAL PRIMARY KEY,
                            user_email VARCHAR(255) NOT NULL,
                            department VARCHAR(255),
                            calculated_at TIMESTAMP NOT NULL,
                            lookback_days INTEGER NOT NULL,
                            if_score DOUBLE PRECISION NOT NULL,
                            anomaly_raw DOUBLE PRECISION NOT NULL,
                            is_anomaly BOOLEAN NOT NULL,
                            incident_count INTEGER NOT NULL,
                            feature_contributions TEXT NOT NULL DEFAULT '[]',
                            group_breakdown TEXT NOT NULL DEFAULT '{}',
                            job_id VARCHAR(50) NOT NULL DEFAULT ''
                        );

                        CREATE INDEX ix_isolation_forest_scores_user_email
                            ON dlp.isolation_forest_scores(user_email);
                        CREATE INDEX ix_isolation_forest_scores_calculated_at
                            ON dlp.isolation_forest_scores(calculated_at);
                        CREATE INDEX ix_isolation_forest_scores_job_id
                            ON dlp.isolation_forest_scores(job_id);
                        CREATE INDEX ix_isolation_forest_scores_is_anomaly
                            ON dlp.isolation_forest_scores(is_anomaly);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS dlp.isolation_forest_scores;
                DROP TABLE IF EXISTS public.isolation_forest_scores;
            ");
        }
    }
}
