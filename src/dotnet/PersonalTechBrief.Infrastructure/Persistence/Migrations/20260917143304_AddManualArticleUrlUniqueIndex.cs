using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualArticleUrlUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_SourceItems_ManualUrl_NormalizedUrlHash",
                table: "SourceItems",
                column: "NormalizedUrlHash",
                unique: true,
                filter: "[OriginType] = 'ManualUrl' AND [NormalizedUrlHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_SourceItems_ManualUrl_NormalizedUrlHash",
                table: "SourceItems");
        }
    }
}
