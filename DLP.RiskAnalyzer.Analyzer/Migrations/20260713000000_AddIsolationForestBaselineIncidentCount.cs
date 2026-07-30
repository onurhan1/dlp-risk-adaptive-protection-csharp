using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLP.RiskAnalyzer.Analyzer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AnalyzerDbContext))]
    [Migration("20260713000000_AddIsolationForestBaselineIncidentCount")]
    public partial class AddIsolationForestBaselineIncidentCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('dlp.isolation_forest_scores') IS NOT NULL THEN
                        ALTER TABLE dlp.isolation_forest_scores
                        ADD COLUMN IF NOT EXISTS baseline_incident_count INTEGER NOT NULL DEFAULT 0;
                    ELSIF to_regclass('public.isolation_forest_scores') IS NOT NULL THEN
                        ALTER TABLE public.isolation_forest_scores
                        ADD COLUMN IF NOT EXISTS baseline_incident_count INTEGER NOT NULL DEFAULT 0;
                    ELSE
                        RAISE EXCEPTION 'isolation_forest_scores table does not exist in dlp or public schema';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE IF EXISTS dlp.isolation_forest_scores
                DROP COLUMN IF EXISTS baseline_incident_count;

                ALTER TABLE IF EXISTS public.isolation_forest_scores
                DROP COLUMN IF EXISTS baseline_incident_count;
            ");
        }
    }
}
