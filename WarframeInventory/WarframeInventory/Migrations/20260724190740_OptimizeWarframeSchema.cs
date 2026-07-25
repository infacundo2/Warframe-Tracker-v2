using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeWarframeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Weapons_UniqueName",
                table: "Weapons");

            migrationBuilder.DropIndex(
                name: "IX_Warframes_UniqueName",
                table: "Warframes");

            migrationBuilder.DropIndex(
                name: "IX_Relics_UniqueName",
                table: "Relics");

            migrationBuilder.DropIndex(
                name: "IX_Mods_UniqueName",
                table: "Mods");

            // Preserve every orphaned inventory row before enforcing user foreign keys.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS OrphanedWarframeInventory (
                    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    SourceTable VARCHAR(64) NOT NULL,
                    SourceId INT NOT NULL,
                    UserId VARCHAR(255) NOT NULL,
                    PayloadJson JSON NOT NULL,
                    ArchivedAtUtc DATETIME(6) NOT NULL
                ) CHARACTER SET utf8mb4;

                INSERT INTO OrphanedWarframeInventory (SourceTable, SourceId, UserId, PayloadJson, ArchivedAtUtc)
                SELECT 'UserWarframes', u.Id, u.UserId,
                       JSON_OBJECT('WarframeUnique', u.WarframeUnique, 'Owned', u.Owned), UTC_TIMESTAMP(6)
                FROM UserWarframes u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;
                DELETE u FROM UserWarframes u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;

                INSERT INTO OrphanedWarframeInventory (SourceTable, SourceId, UserId, PayloadJson, ArchivedAtUtc)
                SELECT 'UserWeapons', u.Id, u.UserId,
                       JSON_OBJECT('WeaponUnique', u.WeaponUnique, 'Owned', u.Owned), UTC_TIMESTAMP(6)
                FROM UserWeapons u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;
                DELETE u FROM UserWeapons u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;

                INSERT INTO OrphanedWarframeInventory (SourceTable, SourceId, UserId, PayloadJson, ArchivedAtUtc)
                SELECT 'UserMods', u.Id, u.UserId,
                       JSON_OBJECT('ModUnique', u.ModUnique, 'Owned', u.Owned, 'Quantity', u.Quantity), UTC_TIMESTAMP(6)
                FROM UserMods u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;
                DELETE u FROM UserMods u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;

                INSERT INTO OrphanedWarframeInventory (SourceTable, SourceId, UserId, PayloadJson, ArchivedAtUtc)
                SELECT 'UserRelics', u.Id, u.UserId,
                       JSON_OBJECT('RelicUnique', u.RelicUnique, 'Quantity', u.Quantity), UTC_TIMESTAMP(6)
                FROM UserRelics u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;
                DELETE u FROM UserRelics u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;

                INSERT INTO OrphanedWarframeInventory (SourceTable, SourceId, UserId, PayloadJson, ArchivedAtUtc)
                SELECT 'UserComponents', u.Id, u.UserId,
                       JSON_OBJECT('ParentUnique', u.ParentUnique, 'ComponentName', u.ComponentName,
                                   'Owned', u.Owned, 'Quantity', u.Quantity), UTC_TIMESTAMP(6)
                FROM UserComponents u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;
                DELETE u FROM UserComponents u LEFT JOIN AspNetUsers a ON a.Id = u.UserId WHERE a.Id IS NULL;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Relics_UniqueName",
                table: "Relics",
                column: "UniqueName");

            migrationBuilder.CreateTable(
                name: "DataSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LastSuccessUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastAttemptUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Error = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSyncStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RelicRewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RelicUnique = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ItemUnique = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ItemName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rarity = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Chance = table.Column<double>(type: "double", nullable: false),
                    MarketUrlName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelicRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelicRewards_Relics_RelicUnique",
                        column: x => x.RelicUnique,
                        principalTable: "Relics",
                        principalColumn: "UniqueName",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_UniqueName",
                table: "Weapons",
                column: "UniqueName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warframes_UniqueName",
                table: "Warframes",
                column: "UniqueName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Relics_UniqueName",
                table: "Relics",
                column: "UniqueName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mods_UniqueName",
                table: "Mods",
                column: "UniqueName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RelicRewards_RelicUnique_ItemUnique",
                table: "RelicRewards",
                columns: new[] { "RelicUnique", "ItemUnique" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserComponents_AspNetUsers_UserId",
                table: "UserComponents",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMods_AspNetUsers_UserId",
                table: "UserMods",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRelics_AspNetUsers_UserId",
                table: "UserRelics",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWarframes_AspNetUsers_UserId",
                table: "UserWarframes",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWeapons_AspNetUsers_UserId",
                table: "UserWeapons",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserComponents_AspNetUsers_UserId",
                table: "UserComponents");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMods_AspNetUsers_UserId",
                table: "UserMods");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRelics_AspNetUsers_UserId",
                table: "UserRelics");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWarframes_AspNetUsers_UserId",
                table: "UserWarframes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWeapons_AspNetUsers_UserId",
                table: "UserWeapons");

            migrationBuilder.DropTable(
                name: "DataSyncStates");

            migrationBuilder.DropTable(
                name: "RelicRewards");

            migrationBuilder.DropIndex(
                name: "IX_Weapons_UniqueName",
                table: "Weapons");

            migrationBuilder.DropIndex(
                name: "IX_Warframes_UniqueName",
                table: "Warframes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Relics_UniqueName",
                table: "Relics");

            migrationBuilder.DropIndex(
                name: "IX_Relics_UniqueName",
                table: "Relics");

            migrationBuilder.DropIndex(
                name: "IX_Mods_UniqueName",
                table: "Mods");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_UniqueName",
                table: "Weapons",
                column: "UniqueName");

            migrationBuilder.CreateIndex(
                name: "IX_Warframes_UniqueName",
                table: "Warframes",
                column: "UniqueName");

            migrationBuilder.CreateIndex(
                name: "IX_Relics_UniqueName",
                table: "Relics",
                column: "UniqueName");

            migrationBuilder.CreateIndex(
                name: "IX_Mods_UniqueName",
                table: "Mods",
                column: "UniqueName");
        }
    }
}
