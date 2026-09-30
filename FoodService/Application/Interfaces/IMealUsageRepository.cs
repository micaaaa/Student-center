using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Application.Interfaces;

public interface IMealUsageRepository
{
    Task<MealEntitlement?> GetEntitlementAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<MealEntitlement>> GetEntitlementsAsync(
        Guid studentId, int year, int month, CancellationToken ct);
    Task<bool> EntitlementExistsAsync(Guid studentId, int year, int month, MealType type, CancellationToken ct);
    Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct);
    Task<MealConsumption?> GetConsumptionAsync(Guid id, CancellationToken ct);
    Task<MealConsumption?> GetByRequestIdAsync(Guid requestId, CancellationToken ct);
    Task<IReadOnlyCollection<MealConsumption>> GetConsumptionsAsync(
        Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct);
    Task AddEntitlementAsync(MealEntitlement entitlement, CancellationToken ct);
    Task AddConsumptionAsync(MealConsumption consumption, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
