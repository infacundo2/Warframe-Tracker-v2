using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.DesktopMigrations
{
    /// <inheritdoc />
    public partial class AddEquipmentMastery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Mastered",
                table: "UserWeapons",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MasteryXp",
                table: "UserWeapons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "Mastered",
                table: "UserWarframes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MasteryXp",
                table: "UserWarframes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mastered",
                table: "UserWeapons");

            migrationBuilder.DropColumn(
                name: "MasteryXp",
                table: "UserWeapons");

            migrationBuilder.DropColumn(
                name: "Mastered",
                table: "UserWarframes");

            migrationBuilder.DropColumn(
                name: "MasteryXp",
                table: "UserWarframes");
        }
    }
}
