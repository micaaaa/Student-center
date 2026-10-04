using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;
using StudentCenter.BillingService.Domain.Exceptions;
using StudentCenter.BillingService.Infrastructure.Persistence;

namespace StudentCenter.BillingService.Infrastructure.Repositories;

public sealed class BillingRepository(BillingDbContext db) : IBillingRepository
{
    public async Task ReceiveAsync(IncomingBillingEvent message, DateTimeOffset now, CancellationToken ct)
    {
        var receipt = await db.ReceivedEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == message.EventId, ct);
        if (receipt is not null)
        {
            EnsureSameEvent(receipt, message);
            return;
        }

        if (message.Accommodation is { } accommodation)
        {
            var reference = await db.AccommodationReferences.SingleOrDefaultAsync(item => item.Id == accommodation.AccommodationId, ct);
            if (reference is null)
            {
                db.AccommodationReferences.Add(new AccommodationReference(accommodation.AccommodationId,
                    accommodation.StudentId, accommodation.RoomId, accommodation.AcademicYear, accommodation.Type));
            }
            else
            {
                if (reference.StudentId != accommodation.StudentId || reference.RoomId != accommodation.RoomId
                    || reference.AcademicYear != accommodation.AcademicYear)
                {
                    throw new ArgumentException("Accommodation ID was reused with different ownership data.");
                }

                reference.Apply(accommodation.Type);
            }
        }
        else if (message.Meal is null)
        {
            throw new ArgumentException("Billing event has no supported payload.");
        }

        db.ReceivedEvents.Add(new ReceivedEvent(message.EventId, message.Fingerprint, now.UtcDateTime));
        try
        {
            // Prepaid meal purchases only record a receipt; they never create outstanding charges.
            // Accommodation reference and receipt are committed atomically before the ACK.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            receipt = await db.ReceivedEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == message.EventId, ct);
            if (receipt is null)
            {
                // Concurrent source creation: let the consumer reconnect and retry with a fresh scope.
                throw;
            }

            EnsureSameEvent(receipt, message);
        }
    }

    public Task<Charge?> GetByRequestAsync(Guid requestId, CancellationToken ct)
    {
        return db.Charges.AsNoTracking().SingleOrDefaultAsync(charge => charge.RequestId == requestId, ct);
    }

    public async Task<Charge> CreateAsync(Charge charge, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var previous = await GetByRequestAsync(charge.RequestId!.Value, ct);
            if (previous is not null)
            {
                EnsureSameRequest(previous, charge);
                return previous;
            }

            if (await db.Charges.AnyAsync(item => item.SourceKey == charge.SourceKey, ct))
            {
                throw new BillingConflictException("A charge already exists for this accommodation and billing period.");
            }

            if (charge.Type == ChargeType.Accommodation)
            {
                var reference = await db.AccommodationReferences.SingleOrDefaultAsync(item => item.Id == charge.ReferenceId, ct);
                if (reference is null)
                {
                    throw new BillingConflictException("Accommodation event has not been received yet. Check the reference and retry after synchronization.");
                }

                if (reference.StudentId != charge.StudentId || reference.Stage == 4)
                {
                    throw new BillingConflictException("Accommodation must belong to this student and must not be cancelled.");
                }
            }

            db.Charges.Add(charge);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return charge;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            var previous = await GetByRequestAsync(charge.RequestId!.Value, ct);
            if (previous is not null)
            {
                EnsureSameRequest(previous, charge);
                return previous;
            }

            throw new BillingConflictException("A charge for this source already exists.");
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            throw new BillingConflictException("Billing data changed concurrently. Retry using the same request ID.");
        }
    }

    public Task<Charge?> GetAsync(Guid id, Guid? studentId, CancellationToken ct)
    {
        return db.Charges.AsNoTracking().SingleOrDefaultAsync(charge => charge.Id == id
            && (!studentId.HasValue || charge.StudentId == studentId), ct);
    }

    public async Task<IReadOnlyCollection<Charge>> ListAsync(Guid studentId, bool overdueOnly,
        DateOnly today, int page, int pageSize, CancellationToken ct)
    {
        return await db.Charges.AsNoTracking().Where(charge => charge.StudentId == studentId
                && (!overdueOnly || charge.DueDate < today && charge.PaidAmount < charge.Amount))
            .OrderByDescending(charge => charge.CreatedAtUtc).ThenByDescending(charge => charge.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public async Task<BalanceResponse> BalanceAsync(Guid studentId, DateOnly today, CancellationToken ct)
    {
        // A single statement keeps totals internally consistent during concurrent inserts.
        var totals = await db.Charges.Where(charge => charge.StudentId == studentId).GroupBy(charge => charge.StudentId)
            .Select(group => new
            {
                Count = group.Count(),
                TotalCharged = group.Sum(charge => charge.Amount),
                TotalPaid = group.Sum(charge => charge.PaidAmount),
                Outstanding = group.Sum(charge => charge.Amount - charge.PaidAmount),
                Overdue = group.Sum(charge => charge.DueDate < today ? charge.Amount - charge.PaidAmount : 0m)
            }).SingleOrDefaultAsync(ct);
        return new BalanceResponse("RSD", totals?.Count ?? 0, totals?.Outstanding ?? 0m, totals?.Overdue ?? 0m,
            totals?.TotalCharged ?? 0m, totals?.TotalPaid ?? 0m);
    }

    private static void EnsureSameEvent(ReceivedEvent existing, IncomingBillingEvent message)
    {
        if (existing.Fingerprint != message.Fingerprint)
        {
            throw new ArgumentException("Event ID was reused with different content.");
        }
    }

    private static void EnsureSameRequest(Charge existing, Charge requested)
    {
        if (existing.SourceFingerprint != requested.SourceFingerprint)
        {
            throw new BillingConflictException("Request ID was already used for a different charge.");
        }
    }

    private static bool IsDeadlock(Exception exception)
    {
        return exception is SqlException { Number: 1205 }
            || exception.InnerException is not null && IsDeadlock(exception.InnerException);
    }
}

