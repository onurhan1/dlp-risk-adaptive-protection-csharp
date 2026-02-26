using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DLP.RiskAnalyzer.Analyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddAIBehavioralAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_behavioral_analyses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    analysis_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    risk_score = table.Column<int>(type: "integer", nullable: false),
                    anomaly_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ai_explanation = table.Column<string>(type: "text", nullable: false),
                    ai_recommendation = table.Column<string>(type: "text", nullable: false),
                    reference_incident_ids = table.Column<string>(type: "text", nullable: false),
                    analysis_metadata = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_behavioral_analyses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_behavioral_analyses_analysis_date",
                table: "ai_behavioral_analyses",
                column: "analysis_date");

            migrationBuilder.CreateIndex(
                name: "IX_ai_behavioral_analyses_anomaly_level",
                table: "ai_behavioral_analyses",
                column: "anomaly_level");

            migrationBuilder.CreateIndex(
                name: "IX_ai_behavioral_analyses_entity_type_entity_id",
                table: "ai_behavioral_analyses",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_behavioral_analyses");
        }
    }
}
