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
