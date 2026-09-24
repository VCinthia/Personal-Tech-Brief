using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTechBrief.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionAndTransactionalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngestionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RetrievedItemCount = table.Column<int>(type: "int", nullable: false),
                    NewItemCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionRuns_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_BIN2"),
                    OriginalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CanonicalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    NormalizedUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true, collation: "Latin1_General_100_BIN2"),
                    NormalizedUrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetrievedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    LastFailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceItems_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngestionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DispatchAttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastDispatchAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDispatchError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_IngestionRuns_IngestionRunId",
                        column: x => x.IngestionRunId,
                        principalTable: "IngestionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionRuns_SourceId_StartedAtUtc",
                table: "IngestionRuns",
                columns: new[] { "SourceId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_IngestionRunId",
                table: "OutboxMessages",
                column: "IngestionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_SourceId",
                table: "OutboxMessages",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAtUtc",
                table: "OutboxMessages",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_OutboxMessages_SourceItemId",
                table: "OutboxMessages",
                column: "SourceItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceItems_ContentHash",
                table: "SourceItems",
                column: "ContentHash",
                filter: "[ContentHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SourceItems_NormalizedTitle_RetrievedAtUtc",
                table: "SourceItems",
                columns: new[] { "NormalizedTitle", "RetrievedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceItems_NormalizedUrlHash",
                table: "SourceItems",
                column: "NormalizedUrlHash",
                filter: "[NormalizedUrlHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SourceItems_SourceId_ExternalId",
                table: "SourceItems",
                columns: new[] { "SourceId", "ExternalId" },
                unique: true,
                filter: "[SourceId] IS NOT NULL AND [ExternalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "IngestionRuns");

            migrationBuilder.DropTable(
                name: "SourceItems");
        }
    }
}
