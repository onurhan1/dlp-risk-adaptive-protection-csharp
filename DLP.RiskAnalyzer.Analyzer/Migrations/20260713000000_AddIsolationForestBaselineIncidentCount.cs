using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLP.RiskAnalyzer.Analyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsolationForestBaselineIncidentCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Resolve the schema that actually holds the table: it is created in "public" by the
            // earlier migration and only moved to "dlp" at startup, so an unqualified ALTER (which
            // only sees "public") fails with 42P01 on any server that has already been relocated.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    tbl regclass := COALESCE(to_regclass('dlp.isolation_forest_scores'),
                                             to_regclass('public.isolation_forest_scores'));
                BEGIN
                    IF tbl IS NULL THEN
                        RETURN;
                    END IF;

                    EXECUTE format(
                        'ALTER TABLE %s ADD COLUMN IF NOT EXISTS baseline_incident_count INTEGER NOT NULL DEFAULT 0', tbl);
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    tbl regclass := COALESCE(to_regclass('dlp.isolation_forest_scores'),
                                             to_regclass('public.isolation_forest_scores'));
                BEGIN
                    IF tbl IS NULL THEN
                        RETURN;
                    END IF;

                    EXECUTE format('ALTER TABLE %s DROP COLUMN IF EXISTS baseline_incident_count', tbl);
                END $$;
            ");
        }
    }
}
