using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.FoodService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntitlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    MealType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPurchases", x => x.Id);
                    table.CheckConstraint("CK_MealPurchases_Amount", "[Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] = [Quantity] * [UnitPrice]");
                    table.ForeignKey(
                        name: "FK_MealPurchases_MealEntitlements_EntitlementId",
                        column: x => x.EntitlementId,
                        principalTable: "MealEntitlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealPurchases_EntitlementId",
                table: "MealPurchases",
                column: "EntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPurchases_RequestId",
                table: "MealPurchases",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPurchases_StudentId_Year_Month_PurchasedAtUtc",
                table: "MealPurchases",
                columns: new[] { "StudentId", "Year", "Month", "PurchasedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealPurchases");
        }
    }
}
