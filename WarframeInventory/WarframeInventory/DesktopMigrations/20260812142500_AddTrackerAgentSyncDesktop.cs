using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.DesktopMigrations
{
    /// <inheritdoc />
    public partial class AddTrackerAgentSyncDesktop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSequence = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentDevices_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentPairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VerifierHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsumedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPairings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentPairings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventorySyncBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsAuthoritative = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapturedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppliedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    ChangedRecords = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySyncBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventorySyncBatches_AgentDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AgentDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventorySyncBatches_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDevices_TokenHash",
                table: "AgentDevices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentDevices_UserId_RevokedUtc",
                table: "AgentDevices",
                columns: new[] { "UserId", "RevokedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPairings_CodeHash",
                table: "AgentPairings",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentPairings_UserId",
                table: "AgentPairings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySyncBatches_DeviceId_Sequence",
                table: "InventorySyncBatches",
                columns: new[] { "DeviceId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventorySyncBatches_UserId_ReceivedUtc",
                table: "InventorySyncBatches",
                columns: new[] { "UserId", "ReceivedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPairings");

            migrationBuilder.DropTable(
                name: "InventorySyncBatches");

            migrationBuilder.DropTable(
                name: "AgentDevices");
        }
    }
}
