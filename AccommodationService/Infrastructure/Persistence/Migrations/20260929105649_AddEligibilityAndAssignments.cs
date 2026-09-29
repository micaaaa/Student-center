using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.AccommodationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEligibilityAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceivedEligibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedEligibilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentAccommodations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EligibilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAccommodations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAccommodations_ReceivedEligibilities_EligibilityId",
                        column: x => x.EligibilityId,
                        principalTable: "ReceivedEligibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAccommodations_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedEligibilities_CompetitionId",
                table: "ReceivedEligibilities",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedEligibilities_EventId",
                table: "ReceivedEligibilities",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentAccommodations_EligibilityId",
                table: "StudentAccommodations",
                column: "EligibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAccommodations_RoomId",
                table: "StudentAccommodations",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAccommodations_StudentId",
                table: "StudentAccommodations",
                column: "StudentId",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentAccommodations");

            migrationBuilder.DropTable(
                name: "ReceivedEligibilities");
        }
    }
}
