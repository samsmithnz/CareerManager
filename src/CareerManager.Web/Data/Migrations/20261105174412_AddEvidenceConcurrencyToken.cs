using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerManager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "CareerEntries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "CareerEntries");
        }
    }
}
