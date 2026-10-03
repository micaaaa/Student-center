using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Domain.Entities;

namespace StudentCenter.BillingService.Application.Interfaces;

public interface IBillingRepository
{
    Task ReceiveAsync(IncomingBillingEvent message, DateTimeOffset now, CancellationToken ct);
    Task<Charge?> GetByRequestAsync(Guid requestId, CancellationToken ct);
    Task<Charge> CreateAsync(Charge charge, CancellationToken ct);
    Task<Charge?> GetAsync(Guid id, Guid? studentId, CancellationToken ct);
    Task<IReadOnlyCollection<Charge>> ListAsync(Guid studentId, bool overdueOnly,
        DateOnly today, int page, int pageSize, CancellationToken ct);
    Task<BalanceResponse> BalanceAsync(Guid studentId, DateOnly today, CancellationToken ct);
}

