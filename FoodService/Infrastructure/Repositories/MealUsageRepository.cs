using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;
using StudentCenter.FoodService.Infrastructure.Persistence;

namespace StudentCenter.FoodService.Infrastructure.Repositories;

public sealed class MealUsageRepository(FoodDbContext db) : IMealUsageRepository
{
    public Task<MealEntitlement?> GetEntitlementAsync(Guid id, CancellationToken ct)
    {
        return db.MealEntitlements.SingleOrDefaultAsync(entitlement => entitlement.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<MealEntitlement>> GetEntitlementsAsync(
        Guid studentId, int year, int month, CancellationToken ct)
    {
        return await db.MealEntitlements.AsNoTracking()
            .Where(entitlement => entitlement.StudentId == studentId && entitlement.Year == year
                && entitlement.Month == month)
            .OrderBy(entitlement => entitlement.MealType).ToArrayAsync(ct);
    }

    public Task<bool> EntitlementExistsAsync(
        Guid studentId, int year, int month, MealType type, CancellationToken ct)
    {
        return db.MealEntitlements.AnyAsync(entitlement => entitlement.StudentId == studentId
            && entitlement.Year == year && entitlement.Month == month && entitlement.MealType == type, ct);
    }

    public Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct)
    {
        return db.Restaurants.SingleOrDefaultAsync(restaurant => restaurant.Id == id, ct);
    }

    public Task<MealConsumption?> GetConsumptionAsync(Guid id, CancellationToken ct)
    {
        return db.MealConsumptions.AsNoTracking().SingleOrDefaultAsync(consumption => consumption.Id == id, ct);
    }

    public Task<MealConsumption?> GetByRequestIdAsync(Guid requestId, CancellationToken ct)
    {
        return db.MealConsumptions.AsNoTracking()
            .SingleOrDefaultAsync(consumption => consumption.RequestId == requestId, ct);
    }

    public async Task<IReadOnlyCollection<MealConsumption>> GetConsumptionsAsync(
        Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        return await db.MealConsumptions.AsNoTracking()
            .Where(consumption => consumption.StudentId == studentId
                && consumption.ConsumedAtUtc >= from && consumption.ConsumedAtUtc < to)
            .OrderByDescending(consumption => consumption.ConsumedAtUtc).ThenBy(consumption => consumption.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public async Task AddEntitlementAsync(MealEntitlement entitlement, CancellationToken ct)
    {
        await db.MealEntitlements.AddAsync(entitlement, ct);
    }

    public async Task AddConsumptionAsync(MealConsumption consumption, CancellationToken ct)
    {
        await db.MealConsumptions.AddAsync(consumption, ct);
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
            throw new FoodConflictException("Meal entitlement changed concurrently. Reload before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new FoodConflictException("Duplicate entitlement or request ID. Retry consumption with the same request ID.");
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
            // EF may wrap a SQL deadlock as a transient execution-strategy failure.
            throw new FoodConflictException("Meal entitlement changed concurrently. Retry with the same request ID.");
        }
    }
}
