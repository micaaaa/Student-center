using Microsoft.EntityFrameworkCore;
using StudentCenter.FoodService.Domain.Entities;

namespace StudentCenter.FoodService.Infrastructure.Persistence;

public sealed class FoodDbContext(DbContextOptions<FoodDbContext> options) : DbContext(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealEntitlement> MealEntitlements => Set<MealEntitlement>();
    public DbSet<MealConsumption> MealConsumptions => Set<MealConsumption>();
    public DbSet<MealPurchase> MealPurchases => Set<MealPurchase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var restaurants = modelBuilder.Entity<Restaurant>();
        restaurants.HasKey(restaurant => restaurant.Id);
        restaurants.Property(restaurant => restaurant.Name).HasMaxLength(200).IsRequired();
        restaurants.Property(restaurant => restaurant.Address).HasMaxLength(250).IsRequired();
        restaurants.Property(restaurant => restaurant.Status).HasConversion<string>().HasMaxLength(20);
        restaurants.Property(restaurant => restaurant.RowVersion).IsRowVersion();

        var menus = modelBuilder.Entity<Menu>();
        menus.HasKey(menu => menu.Id);
        menus.Property(menu => menu.Status).HasConversion<string>().HasMaxLength(20);
        menus.Property(menu => menu.RowVersion).IsRowVersion();
        menus.HasIndex(menu => new { menu.RestaurantId, menu.Date }).IsUnique();
        menus.HasOne<Restaurant>().WithMany().HasForeignKey(menu => menu.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);
        menus.HasMany(menu => menu.Meals).WithOne().HasForeignKey(meal => meal.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
        menus.Navigation(menu => menu.Meals).UsePropertyAccessMode(PropertyAccessMode.Field);

        var meals = modelBuilder.Entity<Meal>();
        meals.HasKey(meal => meal.Id);
        meals.Property(meal => meal.Id).ValueGeneratedNever();
        meals.Property(meal => meal.Name).HasMaxLength(200).IsRequired();
        meals.Property(meal => meal.Description).HasMaxLength(2000);
        meals.Property(meal => meal.Type).HasConversion<string>().HasMaxLength(20);
        meals.Property(meal => meal.Price).HasPrecision(10, 2);
        meals.ToTable(table => table.HasCheckConstraint("CK_Meals_Price", "[Price] >= 0"));

        var entitlements = modelBuilder.Entity<MealEntitlement>();
        entitlements.HasKey(entitlement => entitlement.Id);
        entitlements.Property(entitlement => entitlement.AcademicYear).HasMaxLength(9).IsRequired();
        entitlements.Property(entitlement => entitlement.MealType).HasConversion<string>().HasMaxLength(20);
        entitlements.Property(entitlement => entitlement.Status).HasConversion<string>().HasMaxLength(20);
        entitlements.Property(entitlement => entitlement.RowVersion).IsRowVersion();
        entitlements.Ignore(entitlement => entitlement.RemainingQuantity);
        entitlements.HasIndex(entitlement => new
        {
            entitlement.StudentId,
            entitlement.Year,
            entitlement.Month,
            entitlement.MealType
        }).IsUnique();
        entitlements.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MealEntitlements_Quantities",
                "[AllowedQuantity] > 0 AND [ConsumedQuantity] >= 0 AND [ConsumedQuantity] <= [AllowedQuantity]");
            table.HasCheckConstraint("CK_MealEntitlements_Period",
                "[Year] BETWEEN 1 AND 9998 AND [Month] BETWEEN 1 AND 12");
        });

        var consumptions = modelBuilder.Entity<MealConsumption>();
        consumptions.HasKey(consumption => consumption.Id);
        consumptions.HasIndex(consumption => consumption.RequestId).IsUnique();
        consumptions.HasIndex(consumption => new { consumption.StudentId, consumption.ConsumedAtUtc });
        consumptions.Property(consumption => consumption.MealType).HasConversion<string>().HasMaxLength(20);
        consumptions.Property(consumption => consumption.CardReference).HasMaxLength(100);
        consumptions.HasOne<MealEntitlement>().WithMany().HasForeignKey(consumption => consumption.EntitlementId)
            .OnDelete(DeleteBehavior.Restrict);
        consumptions.HasOne<Restaurant>().WithMany().HasForeignKey(consumption => consumption.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        var purchases = modelBuilder.Entity<MealPurchase>();
        purchases.HasKey(purchase => purchase.Id);
        purchases.HasIndex(purchase => purchase.RequestId).IsUnique();
        purchases.HasIndex(purchase => new { purchase.StudentId, purchase.Year, purchase.Month, purchase.PurchasedAtUtc });
        purchases.Property(purchase => purchase.AcademicYear).HasMaxLength(9).IsRequired();
        purchases.Property(purchase => purchase.MealType).HasConversion<string>().HasMaxLength(20);
        purchases.Property(purchase => purchase.UnitPrice).HasPrecision(10, 2);
        purchases.Property(purchase => purchase.Amount).HasPrecision(18, 2);
        purchases.HasOne<MealEntitlement>().WithMany().HasForeignKey(purchase => purchase.EntitlementId)
            .OnDelete(DeleteBehavior.Restrict);
        purchases.ToTable(table => table.HasCheckConstraint("CK_MealPurchases_Amount",
            "[Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] = [Quantity] * [UnitPrice]"));
    }
}
