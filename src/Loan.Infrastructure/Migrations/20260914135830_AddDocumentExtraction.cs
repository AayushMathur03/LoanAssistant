using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentAuditTrailJson",
                table: "LoanApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DocumentsJson",
                table: "LoanApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentAuditTrailJson",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "DocumentsJson",
                table: "LoanApplications");
        }
    }
}
