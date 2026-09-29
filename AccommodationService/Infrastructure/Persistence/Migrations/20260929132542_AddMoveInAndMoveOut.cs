using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.AccommodationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoveInAndMoveOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoveIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccommodationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MedicalCertificateReference = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoveIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoveIns_StudentAccommodations_AccommodationId",
                        column: x => x.AccommodationId,
                        principalTable: "StudentAccommodations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MoveOuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccommodationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoveOuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoveOuts_StudentAccommodations_AccommodationId",
                        column: x => x.AccommodationId,
                        principalTable: "StudentAccommodations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoveIns_AccommodationId",
                table: "MoveIns",
                column: "AccommodationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoveOuts_AccommodationId",
                table: "MoveOuts",
                column: "AccommodationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoveIns");

            migrationBuilder.DropTable(
                name: "MoveOuts");
        }
    }
}
