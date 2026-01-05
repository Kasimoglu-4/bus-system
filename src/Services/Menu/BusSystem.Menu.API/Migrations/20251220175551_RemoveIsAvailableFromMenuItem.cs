using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusSystem.Menu.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsAvailableFromMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "MenuItem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "MenuItem",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }
    }
}
