using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingStudentRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RegistrationPending",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationPending",
                table: "Users");
        }
    }
}
