using StudentCenter.BillingService.Domain.Entities;

namespace StudentCenter.BillingService.Application.Interfaces;

public interface IBillingOutboxRepository
{
    Task<IReadOnlyCollection<BillingOutboxMessage>> GetPendingAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
