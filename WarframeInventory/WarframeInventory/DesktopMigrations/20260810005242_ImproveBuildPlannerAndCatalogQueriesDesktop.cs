using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.DesktopMigrations
{
    /// <inheritdoc />
    public partial class ImproveBuildPlannerAndCatalogQueriesDesktop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "SavedBuilds",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SavedBuilds",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "SavedBuilds",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_Name",
                table: "Weapons",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_Type",
                table: "Weapons",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Warframes_Name",
                table: "Warframes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Relics_Vaulted_Name",
                table: "Relics",
                columns: new[] { "Vaulted", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Mods_CompatName",
                table: "Mods",
                column: "CompatName");

            migrationBuilder.CreateIndex(
                name: "IX_Mods_Name",
                table: "Mods",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Mods_Polarity",
                table: "Mods",
                column: "Polarity");

            migrationBuilder.CreateIndex(
                name: "IX_Mods_Rarity",
                table: "Mods",
                column: "Rarity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Weapons_Name",
                table: "Weapons");

            migrationBuilder.DropIndex(
                name: "IX_Weapons_Type",
                table: "Weapons");

            migrationBuilder.DropIndex(
                name: "IX_Warframes_Name",
                table: "Warframes");

            migrationBuilder.DropIndex(
                name: "IX_Relics_Vaulted_Name",
                table: "Relics");

            migrationBuilder.DropIndex(
                name: "IX_Mods_CompatName",
                table: "Mods");

            migrationBuilder.DropIndex(
                name: "IX_Mods_Name",
                table: "Mods");

            migrationBuilder.DropIndex(
                name: "IX_Mods_Polarity",
                table: "Mods");

            migrationBuilder.DropIndex(
                name: "IX_Mods_Rarity",
                table: "Mods");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "SavedBuilds");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SavedBuilds");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "SavedBuilds");
        }
    }
}
