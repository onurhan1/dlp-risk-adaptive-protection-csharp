using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLP.RiskAnalyzer.Analyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsolationForestExplanation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The entity is mapped to the "dlp" schema (HasDefaultSchema), but the table is only
            // relocated there at startup — after migrations run — so on a fresh database it is
            // still in "public" at this point. Resolve whichever schema actually holds it instead
            // of relying on search_path: an unqualified ALTER only sees "public" and fails with
            // 42P01 on every server that has already been relocated to "dlp". That failure is
            // swallowed by the migration error handler, so the column silently never lands and
            // every read of the entity dies with "column i.explanation does not exist".
            //
            // explanation_version defaults to 1 so every pre-existing row is correctly marked as
            // "written before the reason layer". The read path returns an empty explanation for
            // those instead of deserializing a mismatched payload into silent zeroes.
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
                        'ALTER TABLE %s ADD COLUMN IF NOT EXISTS explanation_version INTEGER NOT NULL DEFAULT 1', tbl);
                    EXECUTE format(
                        'ALTER TABLE %s ADD COLUMN IF NOT EXISTS explanation TEXT NOT NULL DEFAULT ''{}''', tbl);
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

                    EXECUTE format('ALTER TABLE %s DROP COLUMN IF EXISTS explanation_version', tbl);
                    EXECUTE format('ALTER TABLE %s DROP COLUMN IF EXISTS explanation', tbl);
                END $$;
            ");
        }
    }
}
