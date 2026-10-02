using Microsoft.EntityFrameworkCore;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;

namespace StudentCenter.MaintenanceService.Infrastructure.Repositories;

public sealed class MaintenanceOutboxRepository(MaintenanceDbContext db) : IMaintenanceOutboxRepository
{
    public async Task<IReadOnlyCollection<MaintenanceOutboxMessage>> GetPendingAsync(CancellationToken ct)
    {
        return await db.OutboxMessages.Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.Sequence).Take(50).ToArrayAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }
}


