using NUnit.Framework;
using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Application.Services;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Tests;

public sealed class CatalogTests
{
    private static readonly DateOnly Date = new(2026, 10, 1);

    [TestCase(-1)]
    [TestCase(1.234)]
    [TestCase(100000000)]
    public void MealRejectsInvalidPrice(decimal price)
    {
        Assert.Throws<ArgumentException>(() => new Meal(MealType.Lunch, "Soup", null, price));
    }

    [Test]
    public void MealRejectsUnknownType()
    {
        Assert.Throws<ArgumentException>(() => new Meal((MealType)99, "Soup", null, 100));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void RestaurantRejectsBlankName(string name)
    {
        Assert.Throws<ArgumentException>(() => new Restaurant(name, "Address"));
    }

    [Test]
    public void InvalidRestaurantUpdateDoesNotPartiallyChangeEntity()
    {
        var restaurant = new Restaurant("Original", "Address");

        Assert.Throws<ArgumentException>(() => restaurant.Update("Changed", "", RestaurantStatus.Inactive));

        Assert.That(restaurant.Name, Is.EqualTo("Original"));
        Assert.That(restaurant.Status, Is.EqualTo(RestaurantStatus.Active));
    }

    [Test]
    public void MenuRequiresDateAndMeals()
    {
        Assert.Throws<ArgumentException>(() => new Menu(Guid.NewGuid(), default, [Meal()]));
        Assert.Throws<ArgumentException>(() => new Menu(Guid.NewGuid(), Date, []));
        Assert.Throws<ArgumentException>(() => new Menu(Guid.NewGuid(), Date,
            Enumerable.Range(0, 31).Select(_ => Meal()).ToArray()));
    }

    [Test]
    public void PublishedMenuCannotBeEditedUntilWithdrawn()
    {
        var restaurant = new Restaurant("Restaurant", "Address");
        var menu = new Menu(restaurant.Id, Date, [Meal()]);
        menu.Publish(restaurant);

        Assert.Throws<FoodConflictException>(() => menu.Update(Date, [Meal()]));
        Assert.Throws<FoodConflictException>(() => menu.Publish(restaurant));

        menu.Withdraw();
        menu.Update(Date.AddDays(1), [Meal("Replacement")]);

        Assert.That(menu.Status, Is.EqualTo(MenuStatus.Draft));
        Assert.That(menu.Meals.Single().Name, Is.EqualTo("Replacement"));
        Assert.That(menu.Meals.Single().MenuId, Is.EqualTo(menu.Id));
        Assert.Throws<FoodConflictException>(() => menu.Withdraw());
    }

    [Test]
    public void InactiveRestaurantCannotPublishMenu()
    {
        var restaurant = new Restaurant("Restaurant", "Address");
        var menu = new Menu(restaurant.Id, Date, [Meal()]);
        restaurant.Update(restaurant.Name, restaurant.Address, RestaurantStatus.Inactive);

        Assert.Throws<FoodConflictException>(() => menu.Publish(restaurant));
        Assert.That(menu.Status, Is.EqualTo(MenuStatus.Draft));
    }

    [Test]
    public void MealCannotBeSharedBetweenMenus()
    {
        var meal = Meal();
        var first = new Menu(Guid.NewGuid(), Date, [meal]);

        Assert.Throws<ArgumentException>(() => new Menu(Guid.NewGuid(), Date, [meal]));
        Assert.That(meal.MenuId, Is.EqualTo(first.Id));
    }

    [Test]
    public async Task DraftIsHiddenFromStudentButAvailableForManagement()
    {
        var repository = new MemoryRepository();
        var service = new FoodCatalogService(repository);
        var menu = new Menu(repository.Restaurant.Id, Date, [Meal()]);
        repository.Menus.Add(menu);

        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await service.GetMenuAsync(menu.Id, false, default));
        Assert.That(await service.GetMenusAsync(repository.Restaurant.Id, Date, Date, false, default),
            Is.Empty);
        Assert.That((await service.GetMenuAsync(menu.Id, true, default)).Id, Is.EqualTo(menu.Id));

        await service.PublishMenuAsync(menu.Id, default);

        Assert.That((await service.GetMenuAsync(menu.Id, false, default)).Status,
            Is.EqualTo(MenuStatus.Published));
    }

    [Test]
    public async Task DeactivatedRestaurantHidesPublishedMenuFromStudent()
    {
        var repository = new MemoryRepository();
        var service = new FoodCatalogService(repository);
        var menu = new Menu(repository.Restaurant.Id, Date, [Meal()]);
        menu.Publish(repository.Restaurant);
        repository.Menus.Add(menu);
        repository.Restaurant.Update("Restaurant", "Address", RestaurantStatus.Inactive);

        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await service.GetMenuAsync(menu.Id, false, default));
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await service.GetMenusAsync(repository.Restaurant.Id, Date, Date, false, default));
        Assert.That(await service.GetRestaurantsAsync(false, default), Is.Empty);
        Assert.That((await service.GetMenuAsync(menu.Id, true, default)).Id, Is.EqualTo(menu.Id));
    }

    [Test]
    public void DuplicateDateDoesNotAddAnotherMenu()
    {
        var repository = new MemoryRepository();
        repository.Menus.Add(new Menu(repository.Restaurant.Id, Date, [Meal()]));
        var service = new FoodCatalogService(repository);

        Assert.ThrowsAsync<FoodConflictException>(async () =>
            await service.CreateMenuAsync(repository.Restaurant.Id, Request(Date), default));

        Assert.That(repository.Menus, Has.Count.EqualTo(1));
        Assert.That(repository.SaveCount, Is.Zero);
    }

    [Test]
    public async Task DraftUpdateReplacesMealsWithoutCreatingAnotherMenu()
    {
        var repository = new MemoryRepository();
        var menu = new Menu(repository.Restaurant.Id, Date, [Meal("Old")]);
        repository.Menus.Add(menu);
        var service = new FoodCatalogService(repository);

        var result = await service.UpdateMenuAsync(menu.Id, Request(Date), default);

        Assert.That(result.Id, Is.EqualTo(menu.Id));
        Assert.That(result.Meals.Single().Name, Is.EqualTo("New"));
        Assert.That(repository.SaveCount, Is.EqualTo(1));
        Assert.That(repository.Menus, Has.Count.EqualTo(1));
    }

    [Test]
    public void InvalidDateRangeIsRejected()
    {
        var repository = new MemoryRepository();
        var service = new FoodCatalogService(repository);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetMenusAsync(repository.Restaurant.Id, Date, Date.AddDays(-1), false, default));
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetMenusAsync(repository.Restaurant.Id, Date, Date.AddDays(93), false, default));
    }

    private static Meal Meal(string name = "Lunch")
    {
        return new Meal(MealType.Lunch, name, null, 120);
    }

    private static MenuRequest Request(DateOnly date)
    {
        return new MenuRequest
        {
            Date = date,
            Meals = [new MealRequest { Type = MealType.Lunch, Name = "New", Price = 100 }]
        };
    }

    private sealed class MemoryRepository : IFoodCatalogRepository
    {
        public Restaurant Restaurant { get; } = new("Restaurant", "Address");
        public List<Menu> Menus { get; } = [];
        public int SaveCount { get; private set; }

        public Task<IReadOnlyCollection<Restaurant>> GetRestaurantsAsync(bool includeInactive, CancellationToken ct)
        {
            IReadOnlyCollection<Restaurant> result = includeInactive || Restaurant.Status == RestaurantStatus.Active
                ? [Restaurant]
                : [];
            return Task.FromResult(result);
        }

        public Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(id == Restaurant.Id ? Restaurant : null);
        }

        public Task<IReadOnlyCollection<Menu>> GetMenusAsync(
            Guid restaurantId, DateOnly from, DateOnly to, bool includeDrafts, CancellationToken ct)
        {
            IReadOnlyCollection<Menu> result = Menus.Where(menu => menu.RestaurantId == restaurantId
                && menu.Date >= from && menu.Date <= to
                && (includeDrafts || menu.Status == MenuStatus.Published)).ToArray();
            return Task.FromResult(result);
        }

        public Task<Menu?> GetMenuAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(Menus.SingleOrDefault(menu => menu.Id == id));
        }

        public Task<bool> MenuExistsAsync(Guid restaurantId, DateOnly date, Guid? exceptId, CancellationToken ct)
        {
            return Task.FromResult(Menus.Any(menu => menu.RestaurantId == restaurantId
                && menu.Date == date && menu.Id != exceptId));
        }

        public Task AddRestaurantAsync(Restaurant restaurant, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task AddMenuAsync(Menu menu, CancellationToken ct)
        {
            Menus.Add(menu);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            return action(ct);
        }
    }
}
