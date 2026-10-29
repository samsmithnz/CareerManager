using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_CareerEntries_OwnerId_Id",
                table: "CareerEntries",
                columns: new[] { "OwnerId", "Id" });

            migrationBuilder.CreateTable(
                name: "CareerPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TargetRoles = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Strengths = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Interests = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EvidenceGaps = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Recommendations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    NextActions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Progress = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerPlans", x => x.Id);
                    table.UniqueConstraint("AK_CareerPlans_OwnerId_Id", x => new { x.OwnerId, x.Id });
                    table.ForeignKey(
                        name: "FK_CareerPlans_CareerOwners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "CareerOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerPlanEvidence",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CareerPlanId = table.Column<int>(type: "int", nullable: false),
                    CareerEntryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerPlanEvidence", x => new { x.OwnerId, x.CareerPlanId, x.CareerEntryId });
                    table.ForeignKey(
                        name: "FK_CareerPlanEvidence_CareerEntries_OwnerId_CareerEntryId",
                        columns: x => new { x.OwnerId, x.CareerEntryId },
                        principalTable: "CareerEntries",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CareerPlanEvidence_CareerPlans_OwnerId_CareerPlanId",
                        columns: x => new { x.OwnerId, x.CareerPlanId },
                        principalTable: "CareerPlans",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerPlanEvidence_OwnerId_CareerEntryId",
                table: "CareerPlanEvidence",
                columns: new[] { "OwnerId", "CareerEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerPlans_OwnerId",
                table: "CareerPlans",
                column: "OwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerPlanEvidence");

            migrationBuilder.DropTable(
                name: "CareerPlans");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CareerEntries_OwnerId_Id",
                table: "CareerEntries");
        }
    }
}
