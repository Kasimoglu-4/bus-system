using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusSystem.Identity.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleBasedAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update default value for new users
            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "AdminUser",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manager",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Admin");

            // Keep existing users with their current roles (they already have "Admin")
            // No data migration needed as existing "Admin" role is still valid
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "AdminUser",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Admin",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Manager");
        }
    }
}
