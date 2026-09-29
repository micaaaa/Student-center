using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.AccommodationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccommodationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_StudentAccommodations_AccommodationId",
                        column: x => x.AccommodationId,
                        principalTable: "StudentAccommodations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_AccommodationId_Type",
                table: "OutboxMessages",
                columns: new[] { "AccommodationId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAtUtc_Sequence",
                table: "OutboxMessages",
                columns: new[] { "PublishedAtUtc", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Sequence",
                table: "OutboxMessages",
                column: "Sequence",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
