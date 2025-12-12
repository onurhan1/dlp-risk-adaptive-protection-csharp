using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DLP.RiskAnalyzer.Analyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_summaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_incidents = table.Column<int>(type: "integer", nullable: false),
                    high_risk_count = table.Column<int>(type: "integer", nullable: false),
                    avg_risk_score = table.Column<double>(type: "double precision", nullable: false),
                    unique_users = table.Column<int>(type: "integer", nullable: false),
                    departments_affected = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_summaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "department_summaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    department = table.Column<string>(type: "text", nullable: false),
                    total_incidents = table.Column<int>(type: "integer", nullable: false),
                    high_risk_count = table.Column<int>(type: "integer", nullable: false),
                    avg_risk_score = table.Column<double>(type: "double precision", nullable: false),
                    unique_users = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_summaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_email = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: true),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    data_type = table.Column<string>(type: "text", nullable: true),
                    policy = table.Column<string>(type: "text", nullable: true),
                    channel = table.Column<string>(type: "text", nullable: true),
                    risk_score = table.Column<int>(type: "integer", nullable: true),
                    repeat_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    data_sensitivity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RiskLevel = table.Column<string>(type: "text", nullable: true),
                    RecommendedAction = table.Column<string>(type: "text", nullable: true),
                    IOBs = table.Column<List<string>>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => new { x.id, x.timestamp });
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "user_risk_trends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_email = table.Column<string>(type: "text", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_incidents = table.Column<int>(type: "integer", nullable: false),
                    risk_score = table.Column<int>(type: "integer", nullable: false),
                    trend_direction = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_risk_trends", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_department",
                table: "incidents",
                column: "department");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_risk_score",
                table: "incidents",
                column: "risk_score");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_timestamp",
                table: "incidents",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_user_email",
                table: "incidents",
                column: "user_email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_summaries");

            migrationBuilder.DropTable(
                name: "department_summaries");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "user_risk_trends");
        }
    }
}
