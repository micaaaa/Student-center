using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.FoodService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealEntitlementsAndConsumptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    MealType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AllowedQuantity = table.Column<int>(type: "int", nullable: false),
                    ConsumedQuantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealEntitlements", x => x.Id);
                    table.CheckConstraint("CK_MealEntitlements_Period", "[Year] BETWEEN 1 AND 9998 AND [Month] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_MealEntitlements_Quantities", "[AllowedQuantity] > 0 AND [ConsumedQuantity] >= 0 AND [ConsumedQuantity] <= [AllowedQuantity]");
                });

            migrationBuilder.CreateTable(
                name: "MealConsumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntitlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealConsumptions_MealEntitlements_EntitlementId",
                        column: x => x.EntitlementId,
                        principalTable: "MealEntitlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MealConsumptions_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealConsumptions_EntitlementId",
                table: "MealConsumptions",
                column: "EntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_MealConsumptions_RequestId",
                table: "MealConsumptions",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealConsumptions_RestaurantId",
                table: "MealConsumptions",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_MealConsumptions_StudentId_ConsumedAtUtc",
                table: "MealConsumptions",
                columns: new[] { "StudentId", "ConsumedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MealEntitlements_StudentId_Year_Month_MealType",
                table: "MealEntitlements",
                columns: new[] { "StudentId", "Year", "Month", "MealType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealConsumptions");

            migrationBuilder.DropTable(
                name: "MealEntitlements");
        }
    }
}
