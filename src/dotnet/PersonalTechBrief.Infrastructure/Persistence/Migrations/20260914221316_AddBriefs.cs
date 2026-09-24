using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBriefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Briefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CandidateCount = table.Column<int>(type: "int", nullable: false),
                    SelectedCount = table.Column<int>(type: "int", nullable: false),
                    GenerationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Briefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BriefItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnologyUpdateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    TitleSnapshot = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TopicSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SummarySnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WhyRelevantSnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RelevanceScoreSnapshot = table.Column<double>(type: "float", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelOrAlgorithmVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BriefItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BriefItems_Briefs_BriefId",
                        column: x => x.BriefId,
                        principalTable: "Briefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BriefItems_TechnologyUpdates_TechnologyUpdateId",
                        column: x => x.TechnologyUpdateId,
                        principalTable: "TechnologyUpdates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BriefItemSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleSnapshot = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UrlSnapshot = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BriefItemSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BriefItemSources_BriefItems_BriefItemId",
                        column: x => x.BriefItemId,
                        principalTable: "BriefItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BriefItemSources_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BriefItems_TechnologyUpdateId",
                table: "BriefItems",
                column: "TechnologyUpdateId");

            migrationBuilder.CreateIndex(
                name: "UX_BriefItems_BriefId_Rank",
                table: "BriefItems",
                columns: new[] { "BriefId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_BriefItems_BriefId_TechnologyUpdateId",
                table: "BriefItems",
                columns: new[] { "BriefId", "TechnologyUpdateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BriefItemSources_BriefItemId",
                table: "BriefItemSources",
                column: "BriefItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BriefItemSources_SourceItemId",
                table: "BriefItemSources",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Briefs_GeneratedAtUtc",
                table: "Briefs",
                column: "GeneratedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BriefItemSources");

            migrationBuilder.DropTable(
                name: "BriefItems");

            migrationBuilder.DropTable(
                name: "Briefs");
        }
    }
}
