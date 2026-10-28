using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerOpportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerOpportunities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    JobDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    CompanyContext = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SourceUrls = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    DateFound = table.Column<DateOnly>(type: "date", nullable: true),
                    ApplicationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    SubmissionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NextActionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NextActionDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerOpportunities", x => x.Id);
                    table.UniqueConstraint("AK_CareerOpportunities_OwnerId_Id", x => new { x.OwnerId, x.Id });
                    table.ForeignKey(
                        name: "FK_CareerOpportunities_CareerOwners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "CareerOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OpportunityId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerDocuments_CareerOpportunities_OwnerId_OpportunityId",
                        columns: x => new { x.OwnerId, x.OpportunityId },
                        principalTable: "CareerOpportunities",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CareerDocuments_CareerOwners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "CareerOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerDocuments_OwnerId_OpportunityId",
                table: "CareerDocuments",
                columns: new[] { "OwnerId", "OpportunityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerOpportunities_OwnerId_Status",
                table: "CareerOpportunities",
                columns: new[] { "OwnerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerDocuments");

            migrationBuilder.DropTable(
                name: "CareerOpportunities");
        }
    }
}
