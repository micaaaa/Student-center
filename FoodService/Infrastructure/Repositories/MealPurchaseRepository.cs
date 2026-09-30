using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Exceptions;
using StudentCenter.FoodService.Infrastructure.Persistence;

namespace StudentCenter.FoodService.Infrastructure.Repositories;

public sealed class MealPurchaseRepository(FoodDbContext db) : IMealPurchaseRepository
{
    public Task<MealEntitlement?> GetEntitlementAsync(Guid id, CancellationToken ct)
    {
        return db.MealEntitlements.SingleOrDefaultAsync(entitlement => entitlement.Id == id, ct);
    }

    public Task<MealPurchase?> GetAsync(Guid id, CancellationToken ct)
    {
        return db.MealPurchases.AsNoTracking().SingleOrDefaultAsync(purchase => purchase.Id == id, ct);
    }

    public Task<MealPurchase?> GetByRequestIdAsync(Guid requestId, CancellationToken ct)
    {
        return db.MealPurchases.AsNoTracking()
            .SingleOrDefaultAsync(purchase => purchase.RequestId == requestId, ct);
    }

    public async Task<IReadOnlyCollection<MealPurchase>> GetHistoryAsync(
        Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct)
    {
        // Filter by the entitlement month, including purchases made in advance.
        return await db.MealPurchases.AsNoTracking()
            .Where(purchase => purchase.StudentId == studentId && purchase.Year == year && purchase.Month == month)
            .OrderByDescending(purchase => purchase.PurchasedAtUtc).ThenBy(purchase => purchase.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public async Task AddAsync(MealPurchase purchase, CancellationToken ct)
    {
        await db.MealPurchases.AddAsync(purchase, ct);
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
            throw new FoodConflictException("Meal entitlement changed concurrently. Retry with the same request ID.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new FoodConflictException("Duplicate purchase request ID. Retry with the same request ID.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            throw new FoodConflictException("Meal entitlement changed concurrently. Retry with the same request ID.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new FoodConflictException("Meal entitlement changed concurrently. Retry with the same request ID.");
        }
        catch (InvalidOperationException exception) when (
            exception.InnerException is SqlException { Number: 1205 }
            or DbUpdateException { InnerException: SqlException { Number: 1205 } })
        {
            throw new FoodConflictException("Meal entitlement changed concurrently. Retry with the same request ID.");
        }
    }
}
