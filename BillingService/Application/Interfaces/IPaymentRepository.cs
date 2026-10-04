using StudentCenter.BillingService.Domain.Entities;

namespace StudentCenter.BillingService.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByRequestAsync(Guid requestId, CancellationToken ct);
    Task<Charge?> GetChargeAsync(Guid chargeId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<Payment?> GetAsync(Guid id, Guid? studentId, CancellationToken ct);
    Task<IReadOnlyCollection<Payment>> ListAsync(Guid studentId, Guid? chargeId, int page, int pageSize, CancellationToken ct);
}
