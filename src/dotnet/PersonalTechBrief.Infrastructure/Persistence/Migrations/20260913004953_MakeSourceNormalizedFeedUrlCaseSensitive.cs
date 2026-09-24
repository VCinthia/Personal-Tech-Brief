using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeSourceNormalizedFeedUrlCaseSensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Sources_NormalizedFeedUrl_Enabled",
                table: "Sources");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedFeedUrl",
                table: "Sources",
                type: "nvarchar(850)",
                maxLength: 850,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(850)",
                oldMaxLength: 850);

            migrationBuilder.CreateIndex(
                name: "UX_Sources_NormalizedFeedUrl_Enabled",
                table: "Sources",
                column: "NormalizedFeedUrl",
                unique: true,
                filter: "[IsEnabled] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Sources_NormalizedFeedUrl_Enabled",
                table: "Sources");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedFeedUrl",
                table: "Sources",
                type: "nvarchar(850)",
                maxLength: 850,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(850)",
                oldMaxLength: 850,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "UX_Sources_NormalizedFeedUrl_Enabled",
                table: "Sources",
                column: "NormalizedFeedUrl",
                unique: true,
                filter: "[IsEnabled] = 1");
        }
    }
}
