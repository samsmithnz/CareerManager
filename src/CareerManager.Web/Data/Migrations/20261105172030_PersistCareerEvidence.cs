using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerManager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistCareerEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CareerPlanEvidence_CareerEntries_OwnerId_CareerEntryId",
                table: "CareerPlanEvidence");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "CareerEntries",
                newName: "Context");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFactualReview",
                table: "CareerPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Contribution",
                table: "CareerEntries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "CareerEntries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metric",
                table: "CareerEntries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "CareerEntries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceNote",
                table: "CareerEntries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "CareerEntries",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "CareerEntries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "CareerEntries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFactualReview",
                table: "CareerDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresFactualReview",
                table: "CareerPlans");

            migrationBuilder.DropColumn(
                name: "Contribution",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "Metric",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "SourceNote",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "CareerEntries");

            migrationBuilder.DropColumn(
                name: "RequiresFactualReview",
                table: "CareerDocuments");

            migrationBuilder.RenameColumn(
                name: "Context",
                table: "CareerEntries",
                newName: "Description");

            migrationBuilder.AddForeignKey(
                name: "FK_CareerPlanEvidence_CareerEntries_OwnerId_CareerEntryId",
                table: "CareerPlanEvidence",
                columns: new[] { "OwnerId", "CareerEntryId" },
                principalTable: "CareerEntries",
                principalColumns: new[] { "OwnerId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
