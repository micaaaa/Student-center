using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Exceptions;
using StudentCenter.BillingService.Infrastructure.Persistence;

namespace StudentCenter.BillingService.Infrastructure.Repositories;

public sealed class PaymentRepository(BillingDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByRequestAsync(Guid requestId, CancellationToken ct)
    {
        return db.Payments.AsNoTracking().SingleOrDefaultAsync(payment => payment.RequestId == requestId, ct);
    }

    public Task<Charge?> GetChargeAsync(Guid chargeId, CancellationToken ct)
    {
        return db.Charges.SingleOrDefaultAsync(charge => charge.Id == chargeId, ct);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        await db.Payments.AddAsync(payment, ct);
        var charge = await db.Charges.FindAsync(new object[] { payment.ChargeId }, ct)
            ?? throw new KeyNotFoundException("Charge not found.");
        await db.OutboxMessages.AddRangeAsync(BillingOutboxMessage.ForPayment(payment, charge), ct);
    }

    public Task SaveAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await action(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BillingConflictException("Charge changed concurrently. Retry using the same request ID.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BillingConflictException("This payment request or transaction reference has already been recorded.");
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            throw new BillingConflictException("Payment data changed concurrently. Retry using the same request ID.");
        }
    }

    public Task<Payment?> GetAsync(Guid id, Guid? studentId, CancellationToken ct)
    {
        return db.Payments.AsNoTracking().SingleOrDefaultAsync(payment => payment.Id == id
            && (!studentId.HasValue || payment.StudentId == studentId), ct);
    }

    public async Task<IReadOnlyCollection<Payment>> ListAsync(
        Guid studentId, Guid? chargeId, int page, int pageSize, CancellationToken ct)
    {
        return await db.Payments.AsNoTracking().Where(payment => payment.StudentId == studentId
                && (!chargeId.HasValue || payment.ChargeId == chargeId))
            .OrderByDescending(payment => payment.PaymentDateUtc).ThenByDescending(payment => payment.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    private static bool IsDeadlock(Exception exception)
    {
        return exception is SqlException { Number: 1205 }
            || exception.InnerException is not null && IsDeadlock(exception.InnerException);
    }
}
