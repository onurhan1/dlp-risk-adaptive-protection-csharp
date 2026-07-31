using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLP.RiskAnalyzer.Analyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsolationForestScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS isolation_forest_scores (
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

                CREATE INDEX IF NOT EXISTS ix_isolation_forest_scores_user_email ON isolation_forest_scores(user_email);
                CREATE INDEX IF NOT EXISTS ix_isolation_forest_scores_calculated_at ON isolation_forest_scores(calculated_at);
                CREATE INDEX IF NOT EXISTS ix_isolation_forest_scores_job_id ON isolation_forest_scores(job_id);
                CREATE INDEX IF NOT EXISTS ix_isolation_forest_scores_is_anomaly ON isolation_forest_scores(is_anomaly);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS isolation_forest_scores;");
        }
    }
}
