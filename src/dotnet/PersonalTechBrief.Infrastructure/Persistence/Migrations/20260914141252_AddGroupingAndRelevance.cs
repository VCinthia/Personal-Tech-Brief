using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupingAndRelevance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnologyUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepresentativeTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PrimaryTopic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FirstObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentRelevanceScore = table.Column<double>(type: "float", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyUpdates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnologyUpdateSources",
                columns: table => new
                {
                    TechnologyUpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimilarityScore = table.Column<double>(type: "float", nullable: true),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyUpdateSources", x => new { x.TechnologyUpdateId, x.SourceItemId });
                    table.ForeignKey(
                        name: "FK_TechnologyUpdateSources_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnologyUpdateSources_TechnologyUpdates_TechnologyUpdateId",
                        column: x => x.TechnologyUpdateId,
                        principalTable: "TechnologyUpdates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UpdateInterestMatches",
                columns: table => new
                {
                    TechnologyUpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchStrength = table.Column<double>(type: "float", nullable: false),
                    MatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateInterestMatches", x => new { x.TechnologyUpdateId, x.InterestId });
                    table.ForeignKey(
                        name: "FK_UpdateInterestMatches_Interests_InterestId",
                        column: x => x.InterestId,
                        principalTable: "Interests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpdateInterestMatches_TechnologyUpdates_TechnologyUpdateId",
                        column: x => x.TechnologyUpdateId,
                        principalTable: "TechnologyUpdates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyUpdates_LastObservedAtUtc",
                table: "TechnologyUpdates",
                column: "LastObservedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyUpdates_PrimaryTopic_LastObservedAtUtc",
                table: "TechnologyUpdates",
                columns: new[] { "PrimaryTopic", "LastObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TechnologyUpdateSources_SourceItemId",
                table: "TechnologyUpdateSources",
                column: "SourceItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UpdateInterestMatches_InterestId",
                table: "UpdateInterestMatches",
                column: "InterestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnologyUpdateSources");

            migrationBuilder.DropTable(
                name: "UpdateInterestMatches");

            migrationBuilder.DropTable(
                name: "TechnologyUpdates");
        }
    }
}
