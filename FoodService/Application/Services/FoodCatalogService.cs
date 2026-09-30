using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Application.Services;

public sealed class FoodCatalogService(IFoodCatalogRepository repository)
{
    public async Task<IReadOnlyCollection<RestaurantResponse>> GetRestaurantsAsync(
        bool includeInactive, CancellationToken ct)
    {
        var restaurants = await repository.GetRestaurantsAsync(includeInactive, ct);
        return restaurants.Select(ToResponse).ToArray();
    }

    public async Task<RestaurantResponse> GetRestaurantAsync(Guid id, bool includeInactive, CancellationToken ct)
    {
        var restaurant = await FindRestaurantAsync(id, ct);
        EnsureVisible(restaurant, includeInactive);
        return ToResponse(restaurant);
    }

    public Task<RestaurantResponse> CreateRestaurantAsync(RestaurantRequest request, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var restaurant = new Restaurant(request.Name, request.Address);
            restaurant.Update(request.Name, request.Address, request.Status);
            await repository.AddRestaurantAsync(restaurant, token);
            await repository.SaveAsync(token);
            return ToResponse(restaurant);
        }, ct);
    }

    public Task<RestaurantResponse> UpdateRestaurantAsync(Guid id, RestaurantRequest request, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var restaurant = await FindRestaurantAsync(id, token);
            restaurant.Update(request.Name, request.Address, request.Status);
            await repository.SaveAsync(token);
            return ToResponse(restaurant);
        }, ct);
    }

    public async Task<IReadOnlyCollection<MenuResponse>> GetMenusAsync(
        Guid restaurantId, DateOnly from, DateOnly to, bool includeDrafts, CancellationToken ct)
    {
        if (from == default || to == default || to < from || to.DayNumber - from.DayNumber > 92)
        {
            throw new ArgumentException("Specify a valid date range of at most 93 days.");
        }

        var restaurant = await FindRestaurantAsync(restaurantId, ct);
        EnsureVisible(restaurant, includeDrafts);
        var menus = await repository.GetMenusAsync(restaurantId, from, to, includeDrafts, ct);
        return menus.Select(ToResponse).ToArray();
    }

    public async Task<MenuResponse> GetMenuAsync(Guid id, bool includeDrafts, CancellationToken ct)
    {
        var menu = await FindMenuAsync(id, ct);
        var restaurant = await FindRestaurantAsync(menu.RestaurantId, ct);
        EnsureVisible(restaurant, includeDrafts);
        if (!includeDrafts && menu.Status != MenuStatus.Published)
        {
            throw new KeyNotFoundException("Menu not found.");
        }

        return ToResponse(menu);
    }

    public Task<MenuResponse> CreateMenuAsync(Guid restaurantId, MenuRequest request, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            await FindRestaurantAsync(restaurantId, token);
            var menu = new Menu(restaurantId, request.Date, CreateMeals(request));
            await EnsureUniqueDateAsync(menu, token);
            await repository.AddMenuAsync(menu, token);
            await repository.SaveAsync(token);
            return ToResponse(menu);
        }, ct);
    }

    public Task<MenuResponse> UpdateMenuAsync(Guid id, MenuRequest request, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var menu = await FindMenuAsync(id, token);
            menu.Update(request.Date, CreateMeals(request));
            await EnsureUniqueDateAsync(menu, token);
            await repository.SaveAsync(token);
            return ToResponse(menu);
        }, ct);
    }

    public Task<MenuResponse> PublishMenuAsync(Guid id, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var menu = await FindMenuAsync(id, token);
            var restaurant = await FindRestaurantAsync(menu.RestaurantId, token);
            menu.Publish(restaurant);
            await repository.SaveAsync(token);
            return ToResponse(menu);
        }, ct);
    }

    public Task<MenuResponse> WithdrawMenuAsync(Guid id, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var menu = await FindMenuAsync(id, token);
            menu.Withdraw();
            await repository.SaveAsync(token);
            return ToResponse(menu);
        }, ct);
    }

    private async Task<Restaurant> FindRestaurantAsync(Guid id, CancellationToken ct)
    {
        return await repository.GetRestaurantAsync(id, ct)
            ?? throw new KeyNotFoundException("Restaurant not found.");
    }

    private async Task<Menu> FindMenuAsync(Guid id, CancellationToken ct)
    {
        return await repository.GetMenuAsync(id, ct)
            ?? throw new KeyNotFoundException("Menu not found.");
    }

    private async Task EnsureUniqueDateAsync(Menu menu, CancellationToken ct)
    {
        if (await repository.MenuExistsAsync(menu.RestaurantId, menu.Date, menu.Id, ct))
        {
            throw new FoodConflictException("A menu already exists for this restaurant and date.");
        }
    }

    private static void EnsureVisible(Restaurant restaurant, bool includeInactive)
    {
        if (!includeInactive && restaurant.Status != RestaurantStatus.Active)
        {
            throw new KeyNotFoundException("Restaurant not found.");
        }
    }

    private static Meal[] CreateMeals(MenuRequest request)
    {
        if (request.Meals is null || request.Meals.Any(meal => meal is null))
        {
            throw new ArgumentException("Meals are required.");
        }

        return request.Meals.Select(meal =>
            new Meal(meal.Type, meal.Name, meal.Description, meal.Price)).ToArray();
    }

    private static RestaurantResponse ToResponse(Restaurant restaurant)
    {
        return new RestaurantResponse(restaurant.Id, restaurant.Name, restaurant.Address, restaurant.Status);
    }

    private static MenuResponse ToResponse(Menu menu)
    {
        return new MenuResponse(menu.Id, menu.RestaurantId, menu.Date, menu.Status,
            menu.Meals.OrderBy(meal => meal.Type).ThenBy(meal => meal.Name).ThenBy(meal => meal.Id)
                .Select(meal => new MealResponse(meal.Id, meal.Type, meal.Name, meal.Description, meal.Price))
                .ToArray());
    }
}
