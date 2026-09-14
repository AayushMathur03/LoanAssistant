using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanApplications",
                columns: table => new
                {
                    ApplicationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApplicantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Facts = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductRules = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Indicators = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanApplications", x => x.ApplicationId);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    RecommendationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DecisionRecommendation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RiskScore = table.Column<double>(type: "float", nullable: false),
                    SummaryReasoning = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Citations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyViolations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissingEvidenceItems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditTrail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedByOfficerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OfficerDecisionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.RecommendationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanApplications");

            migrationBuilder.DropTable(
                name: "Recommendations");
        }
    }
}
