using StudentCenter.FoodService.Domain.Entities;

namespace StudentCenter.FoodService.Application.Interfaces;

public interface IMealPurchaseRepository
{
    Task<MealEntitlement?> GetEntitlementAsync(Guid id, CancellationToken ct);
    Task<MealPurchase?> GetAsync(Guid id, CancellationToken ct);
    Task<MealPurchase?> GetByRequestIdAsync(Guid requestId, CancellationToken ct);
    Task<IReadOnlyCollection<MealPurchase>> GetHistoryAsync(
        Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct);
    Task AddAsync(MealPurchase purchase, CancellationToken ct);
    Task AddEventAsync(FoodOutboxMessage message, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
