using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "CareerDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedContentHash",
                table: "CareerDocuments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "CareerDocuments");

            migrationBuilder.DropColumn(
                name: "ApprovedContentHash",
                table: "CareerDocuments");
        }
    }
}
