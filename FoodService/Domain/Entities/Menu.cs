using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Domain.Entities;

public sealed class Menu
{
    private readonly List<Meal> _meals = [];

    private Menu()
    {
    }

    public Menu(Guid restaurantId, DateOnly date, IReadOnlyCollection<Meal> meals)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException("Restaurant is required.");
        }

        Id = Guid.NewGuid();
        RestaurantId = restaurantId;
        Status = MenuStatus.Draft;
        Update(date, meals);
    }

    public Guid Id { get; private set; }
    public Guid RestaurantId { get; private set; }
    public DateOnly Date { get; private set; }
    public MenuStatus Status { get; private set; }
    public IReadOnlyCollection<Meal> Meals => _meals.AsReadOnly();
    public byte[] RowVersion { get; private set; } = [];

    public void Update(DateOnly date, IReadOnlyCollection<Meal> meals)
    {
        if (Status != MenuStatus.Draft)
        {
            throw new FoodConflictException("Withdraw the published menu before editing it.");
        }

        if (date == default)
        {
            throw new ArgumentException("Menu date is required.");
        }

        if (meals is null || meals.Count is < 1 or > 30
            || meals.Any(meal => meal is null)
            || meals.Select(meal => meal.Id).Distinct().Count() != meals.Count)
        {
            throw new ArgumentException("A menu must contain between 1 and 30 distinct meals.");
        }

        if (meals.Any(meal => meal.MenuId != Guid.Empty && meal.MenuId != Id))
        {
            throw new ArgumentException("Meal already belongs to another menu.");
        }

        var replacements = meals.ToArray();
        Date = date;
        _meals.Clear();
        foreach (var meal in replacements)
        {
            meal.AttachTo(Id);
            _meals.Add(meal);
        }
    }

    public void Publish(Restaurant restaurant)
    {
        if (restaurant.Id != RestaurantId || restaurant.Status != RestaurantStatus.Active)
        {
            throw new FoodConflictException("Only menus of an active restaurant can be published.");
        }

        if (Status != MenuStatus.Draft)
        {
            throw new FoodConflictException("Menu is already published.");
        }

        Status = MenuStatus.Published;
    }

    public void Withdraw()
    {
        if (Status != MenuStatus.Published)
        {
            throw new FoodConflictException("Only a published menu can be withdrawn.");
        }

        Status = MenuStatus.Draft;
    }
}
