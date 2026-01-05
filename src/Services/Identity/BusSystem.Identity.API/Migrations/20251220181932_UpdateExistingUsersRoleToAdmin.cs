using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusSystem.Identity.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingUsersRoleToAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update ALL users to have 'Admin' role
            migrationBuilder.Sql(@"
                UPDATE AdminUser 
                SET Role = 'Admin';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
