using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentProcessingInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentProcessingInbox",
                columns: table => new
                {
                    SourceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvelopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentProcessingInbox", x => x.SourceItemId);
                    table.ForeignKey(
                        name: "FK_ContentProcessingInbox_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentProcessingInbox_Status_ReceivedAtUtc_SourceItemId",
                table: "ContentProcessingInbox",
                columns: new[] { "Status", "ReceivedAtUtc", "SourceItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentProcessingInbox");
        }
    }
}
