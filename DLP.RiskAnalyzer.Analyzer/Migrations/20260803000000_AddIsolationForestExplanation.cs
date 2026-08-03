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
            // explanation_version defaults to 1 so every pre-existing row is correctly marked as
            // "written before the reason layer". The read path returns an empty explanation for
            // those instead of deserializing a mismatched payload into silent zeroes.
            migrationBuilder.Sql(@"
                ALTER TABLE isolation_forest_scores
                ADD COLUMN IF NOT EXISTS explanation_version INTEGER NOT NULL DEFAULT 1;

                ALTER TABLE isolation_forest_scores
                ADD COLUMN IF NOT EXISTS explanation TEXT NOT NULL DEFAULT '{}';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE isolation_forest_scores
                DROP COLUMN IF EXISTS explanation_version;

                ALTER TABLE isolation_forest_scores
                DROP COLUMN IF EXISTS explanation;
            ");
        }
    }
}
