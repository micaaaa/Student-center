using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.BillingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccommodationReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                    table.CheckConstraint("CK_Charges_PositiveAmount", "[Amount] > 0");
                });

            migrationBuilder.CreateTable(
                name: "ReceivedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationReferences_StudentId",
                table: "AccommodationReferences",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_RequestId",
                table: "Charges",
                column: "RequestId",
                unique: true,
                filter: "[RequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SourceKey",
                table: "Charges",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charges_StudentId_DueDate",
                table: "Charges",
                columns: new[] { "StudentId", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccommodationReferences");

            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropTable(
                name: "ReceivedEvents");
        }
    }
}
