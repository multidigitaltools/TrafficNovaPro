using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrafficNova.Data;

#nullable disable

namespace TrafficNova.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260518000001_InitialCreate")]
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Campaigns",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                TargetUrlsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                ThreadCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                VisitTarget = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 100),
                DwellMin = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 15000),
                DwellMax = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 60000),
                BounceRate = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.3),
                ReferrerMode = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "None"),
                CustomReferrer = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                ReferrerKeywords = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                UserAgentMode = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Desktop"),
                CustomUserAgent = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                DeviceType = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Desktop"),
                WindowSize = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "1920x1080"),
                BrowserLanguage = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "en-US"),
                Timezone = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "America/New_York"),
                AcceptCookies = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                JavaScriptEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                UseProxy = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                ProxyGroupTag = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                ProxyRotation = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "RoundRobin"),
                RecordSessions = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                CustomHeadersJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Idle"),
                TotalVisits = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                SuccessVisits = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CookiesBlob = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Campaigns", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ProxyEntries",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Host = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                Port = table.Column<int>(type: "INTEGER", nullable: false),
                Protocol = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Http"),
                Username = table.Column<string>(type: "TEXT", nullable: true),
                Password = table.Column<string>(type: "TEXT", nullable: true),
                Label = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                GroupTag = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                LastTestedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastTestOk = table.Column<bool>(type: "INTEGER", nullable: true),
                AvgResponseMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                SuccessCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                FailureCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProxyEntries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrafficSessions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                ProxyId = table.Column<int>(type: "INTEGER", nullable: true),
                TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                UserAgent = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                Referrer = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Success = table.Column<bool>(type: "INTEGER", nullable: false),
                StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                DwellMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                ScreenshotPath = table.Column<string>(type: "TEXT", nullable: true),
                TracePath = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrafficSessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ScheduledJobs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                CronExpression = table.Column<string>(type: "TEXT", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                NextRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                RunCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                MaxRuns = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ScheduledJobs_Campaigns_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "Campaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TrafficSessions_CampaignId",
            table: "TrafficSessions",
            column: "CampaignId");

        migrationBuilder.CreateIndex(
            name: "IX_TrafficSessions_StartedAt",
            table: "TrafficSessions",
            column: "StartedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobs_CampaignId",
            table: "ScheduledJobs",
            column: "CampaignId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ScheduledJobs");
        migrationBuilder.DropTable(name: "TrafficSessions");
        migrationBuilder.DropTable(name: "ProxyEntries");
        migrationBuilder.DropTable(name: "Campaigns");
    }
}
