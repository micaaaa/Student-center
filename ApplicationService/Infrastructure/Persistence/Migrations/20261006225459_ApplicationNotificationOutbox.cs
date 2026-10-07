using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.ApplicationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationNotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_EligibilityId",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "EligibilityId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EligibilityId",
                table: "OutboxMessages",
                column: "EligibilityId",
                unique: true,
                filter: "[EligibilityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_EligibilityId",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "EligibilityId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EligibilityId",
                table: "OutboxMessages",
                column: "EligibilityId",
                unique: true);
        }
    }
}
