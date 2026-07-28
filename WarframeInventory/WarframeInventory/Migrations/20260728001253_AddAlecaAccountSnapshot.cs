using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.Migrations
{
    /// <inheritdoc />
    public partial class AddAlecaAccountSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlecaAccountSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Platinum = table.Column<int>(type: "int", nullable: true),
                    Credits = table.Column<long>(type: "bigint", nullable: true),
                    Endo = table.Column<int>(type: "int", nullable: true),
                    Ducats = table.Column<int>(type: "int", nullable: true),
                    Aya = table.Column<int>(type: "int", nullable: true),
                    MasteryRank = table.Column<int>(type: "int", nullable: true),
                    CompletionPercentage = table.Column<int>(type: "int", nullable: true),
                    RelicsOpened = table.Column<int>(type: "int", nullable: true),
                    TradeCount = table.Column<int>(type: "int", nullable: true),
                    PublicUsername = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Permissions = table.Column<int>(type: "int", nullable: false),
                    SourceTimestampUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SyncedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlecaAccountSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlecaAccountSnapshots_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AlecaAccountSnapshots_UserId",
                table: "AlecaAccountSnapshots",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlecaAccountSnapshots");
        }
    }
}
