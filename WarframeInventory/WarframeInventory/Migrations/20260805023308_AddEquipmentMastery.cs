using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.Migrations
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
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MasteryXp",
                table: "UserWeapons",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "Mastered",
                table: "UserWarframes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MasteryXp",
                table: "UserWarframes",
                type: "bigint",
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
