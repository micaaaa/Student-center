using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.FoodService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowEmptyMealEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MealEntitlements_Quantities",
                table: "MealEntitlements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MealEntitlements_Quantities",
                table: "MealEntitlements",
                sql: "[AllowedQuantity] >= 0 AND [ConsumedQuantity] >= 0 AND [ConsumedQuantity] <= [AllowedQuantity]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MealEntitlements_Quantities",
                table: "MealEntitlements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MealEntitlements_Quantities",
                table: "MealEntitlements",
                sql: "[AllowedQuantity] > 0 AND [ConsumedQuantity] >= 0 AND [ConsumedQuantity] <= [AllowedQuantity]");
        }
    }
}
