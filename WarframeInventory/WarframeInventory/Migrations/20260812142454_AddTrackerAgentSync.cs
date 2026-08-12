using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackerAgentSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AgentPairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CodeHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VerifierHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceName = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ApprovedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConsumedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InventorySyncBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeviceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAuthoritative = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CapturedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AppliedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedRecords = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
