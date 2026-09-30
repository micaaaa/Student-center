using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Domain.Entities;

public sealed class Meal
{
    private Meal()
    {
    }

    public Meal(MealType type, string name, string? description, decimal price)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Unknown meal type.");
        }

        var cleanName = Restaurant.RequiredText(name, 200, nameof(Name));
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleanDescription?.Length > 2000)
        {
            throw new ArgumentException("Meal description must not exceed 2000 characters.");
        }

        if (price < 0 || price > 99999999.99m || decimal.Round(price, 2) != price)
        {
            throw new ArgumentException("Price must be between 0 and 99999999.99 with at most two decimal places.");
        }

        Id = Guid.NewGuid();
        Type = type;
        Name = cleanName;
        Description = cleanDescription;
        Price = price;
    }

    public Guid Id { get; private set; }
    public Guid MenuId { get; private set; }
    public MealType Type { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }

    internal void AttachTo(Guid menuId)
    {
        if (MenuId != Guid.Empty && MenuId != menuId)
        {
            throw new ArgumentException("Meal already belongs to another menu.");
        }

        MenuId = menuId;
    }
}
