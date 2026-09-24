using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInteractionsAndEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedUpdates",
                columns: table => new
                {
                    TechnologyUpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedUpdates", x => x.TechnologyUpdateId);
                    table.ForeignKey(
                        name: "FK_SavedUpdates_TechnologyUpdates_TechnologyUpdateId",
                        column: x => x.TechnologyUpdateId,
                        principalTable: "TechnologyUpdates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceOpenEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnologyUpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceOpenEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceOpenEvents_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceOpenEvents_TechnologyUpdates_TechnologyUpdateId",
                        column: x => x.TechnologyUpdateId,
                        principalTable: "TechnologyUpdates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnologyUpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FeedbackType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFeedback_BriefItems_BriefItemId",
                        column: x => x.BriefItemId,
                        principalTable: "BriefItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFeedback_TechnologyUpdates_TechnologyUpdateId",
                        column: x => x.TechnologyUpdateId,
                        principalTable: "TechnologyUpdates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceOpenEvents_SourceItemId",
                table: "SourceOpenEvents",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceOpenEvents_TechnologyUpdateId",
                table: "SourceOpenEvents",
                column: "TechnologyUpdateId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedback_BriefItemId",
                table: "UserFeedback",
                column: "BriefItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedback_TechnologyUpdateId_CreatedAtUtc",
                table: "UserFeedback",
                columns: new[] { "TechnologyUpdateId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedUpdates");

            migrationBuilder.DropTable(
                name: "SourceOpenEvents");

            migrationBuilder.DropTable(
                name: "UserFeedback");
        }
    }
}
