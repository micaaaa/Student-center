using StudentCenter.FoodService.Domain.Entities;

namespace StudentCenter.FoodService.Application.Interfaces;

public interface IFoodCatalogRepository
{
    Task<IReadOnlyCollection<Restaurant>> GetRestaurantsAsync(bool includeInactive, CancellationToken ct);
    Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<Menu>> GetMenusAsync(
        Guid restaurantId, DateOnly from, DateOnly to, bool includeDrafts, CancellationToken ct);
    Task<Menu?> GetMenuAsync(Guid id, CancellationToken ct);
    Task<bool> MenuExistsAsync(Guid restaurantId, DateOnly date, Guid? exceptId, CancellationToken ct);
    Task AddRestaurantAsync(Restaurant restaurant, CancellationToken ct);
    Task AddMenuAsync(Menu menu, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
