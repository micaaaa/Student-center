using Microsoft.EntityFrameworkCore;
using StudentCenter.FoodService.Domain.Entities;

namespace StudentCenter.FoodService.Infrastructure.Persistence;

public sealed class FoodDbContext(DbContextOptions<FoodDbContext> options) : DbContext(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Meal> Meals => Set<Meal>();

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
    }
}
