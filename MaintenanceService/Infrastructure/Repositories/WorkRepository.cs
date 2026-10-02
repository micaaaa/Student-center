using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;

namespace StudentCenter.MaintenanceService.Infrastructure.Repositories;

public sealed class WorkRepository(MaintenanceDbContext db) : IWorkRepository
{
    public Task<MaintenanceWorker?> GetWorkerAsync(Guid id, CancellationToken ct)
    {
        return db.Workers.SingleOrDefaultAsync(worker => worker.Id == id, ct);
    }

    public Task<MaintenanceWorker?> GetWorkerByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return db.Workers.SingleOrDefaultAsync(worker => worker.UserId == userId, ct);
    }

    public async Task<IReadOnlyCollection<MaintenanceWorker>> GetWorkersAsync(int page, int pageSize, CancellationToken ct)
    {
        return await db.Workers.AsNoTracking().OrderBy(worker => worker.Name).ThenBy(worker => worker.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public Task<bool> HasOpenAssignmentsAsync(Guid workerId, CancellationToken ct)
    {
        return db.Requests.AnyAsync(request => request.AssignedWorkerId == workerId
            && (request.Status == RequestStatus.Assigned || request.Status == RequestStatus.InProgress), ct);
    }

    public async Task AddWorkerAsync(MaintenanceWorker worker, CancellationToken ct)
    {
        await db.Workers.AddAsync(worker, ct);
    }

    public Task<MaintenanceRequest?> GetRequestAsync(Guid id, CancellationToken ct)
    {
        return db.Requests.SingleOrDefaultAsync(request => request.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<MaintenanceRequest>> GetAssignedRequestsAsync(
        Guid workerId, RequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        return await db.Requests.AsNoTracking().Where(request => request.AssignedWorkerId == workerId
            && (!status.HasValue || request.Status == status))
            .OrderByDescending(request => request.CreatedAtUtc).ThenBy(request => request.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<MaintenanceAction>> GetActionsAsync(
        Guid requestId, int page, int pageSize, CancellationToken ct)
    {
        return await db.Actions.AsNoTracking().Where(action => action.RequestId == requestId)
            .OrderBy(action => action.Sequence).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public async Task AddActionAsync(MaintenanceAction action, CancellationToken ct)
    {
        await db.Actions.AddAsync(action, ct);
        if (action.Type is MaintenanceActionType.Assigned or MaintenanceActionType.Resolved)
        {
            var request = await db.Requests.FindAsync(new object[] { action.RequestId }, ct)
                ?? throw new KeyNotFoundException("Maintenance request not found.");
            var worker = await db.Workers.FindAsync(new object[] { action.WorkerId }, ct)
                ?? throw new KeyNotFoundException("Worker not found.");
            await db.OutboxMessages.AddAsync(MaintenanceOutboxMessage.From(action, request, worker), ct);
        }
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
            throw new MaintenanceConflictException("Maintenance data changed concurrently. Reload before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new MaintenanceConflictException("A worker profile for this account already exists.");
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
