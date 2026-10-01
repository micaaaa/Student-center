using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;

namespace StudentCenter.MaintenanceService.Infrastructure.Repositories;

public sealed class MaintenanceRepository(MaintenanceDbContext db) : IMaintenanceRepository
{
    public async Task<IReadOnlyCollection<MaintenanceCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct)
    {
        return await db.Categories.AsNoTracking().Where(category => includeInactive || category.IsActive)
            .OrderBy(category => category.Name).ThenBy(category => category.Id).ToArrayAsync(ct);
    }

    public Task<MaintenanceCategory?> GetCategoryAsync(Guid id, CancellationToken ct)
    {
        return db.Categories.SingleOrDefaultAsync(category => category.Id == id, ct);
    }

    public Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        return db.Categories.AnyAsync(category => category.Name == name
            && (!exceptId.HasValue || category.Id != exceptId.Value), ct);
    }

    public async Task AddCategoryAsync(MaintenanceCategory category, CancellationToken ct)
    {
        await db.Categories.AddAsync(category, ct);
    }

    public Task<MaintenanceRequest?> GetRequestAsync(Guid id, CancellationToken ct)
    {
        return db.Requests.SingleOrDefaultAsync(request => request.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<MaintenanceRequest>> GetRequestsAsync(
        Guid? studentId, RequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        return await db.Requests.AsNoTracking()
            .Where(request => (!studentId.HasValue || request.StudentId == studentId)
                && (!status.HasValue || request.Status == status))
            .OrderByDescending(request => request.CreatedAtUtc).ThenBy(request => request.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public async Task AddRequestAsync(MaintenanceRequest request, CancellationToken ct)
    {
        await db.Requests.AddAsync(request, ct);
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
            throw new MaintenanceConflictException("The record changed concurrently. Reload before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new MaintenanceConflictException("A category with this name already exists.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            throw new MaintenanceConflictException("Maintenance data changed concurrently. Reload before retrying.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new MaintenanceConflictException("Maintenance data changed concurrently. Reload before retrying.");
        }
        catch (InvalidOperationException exception) when (
            exception.InnerException is SqlException { Number: 1205 }
            or DbUpdateException { InnerException: SqlException { Number: 1205 } })
        {
            throw new MaintenanceConflictException("Maintenance data changed concurrently. Reload before retrying.");
        }
    }
}
