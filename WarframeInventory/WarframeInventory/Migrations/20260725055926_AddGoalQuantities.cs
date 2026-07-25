using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarframeInventory.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DesiredQuantity",
                table: "UserGoals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesiredQuantity",
                table: "UserGoals");
        }
    }
}
