using StudentCenter.FoodService.Domain.Entities;

namespace StudentCenter.FoodService.Application.Interfaces;

public interface IFoodOutboxRepository
{
    Task<IReadOnlyCollection<FoodOutboxMessage>> GetPendingAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

