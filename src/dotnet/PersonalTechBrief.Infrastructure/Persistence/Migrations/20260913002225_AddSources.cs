using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FeedUrl = table.Column<string>(type: "nvarchar(850)", maxLength: 850, nullable: false),
                    NormalizedFeedUrl = table.Column<string>(type: "nvarchar(850)", maxLength: 850, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ETag = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastIngestionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastIngestionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

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
            migrationBuilder.DropTable(
                name: "Sources");
        }
    }
}
