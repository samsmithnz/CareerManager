using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "CareerDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "CareerDocuments",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "CareerDocuments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "CareerDocuments",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CareerDocuments_OwnerId_Id",
                table: "CareerDocuments",
                columns: new[] { "OwnerId", "Id" });

            migrationBuilder.CreateTable(
                name: "CareerDocumentEvidence",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CareerDocumentId = table.Column<int>(type: "int", nullable: false),
                    CareerEntryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerDocumentEvidence", x => new { x.OwnerId, x.CareerDocumentId, x.CareerEntryId });
                    table.ForeignKey(
                        name: "FK_CareerDocumentEvidence_CareerDocuments_OwnerId_CareerDocumentId",
                        columns: x => new { x.OwnerId, x.CareerDocumentId },
                        principalTable: "CareerDocuments",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerDocumentEvidence_OwnerId_CareerEntryId",
                table: "CareerDocumentEvidence",
                columns: new[] { "OwnerId", "CareerEntryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerDocumentEvidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CareerDocuments_OwnerId_Id",
                table: "CareerDocuments");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "CareerDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "CareerDocuments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "CareerDocuments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "CareerDocuments");
        }
    }
}
