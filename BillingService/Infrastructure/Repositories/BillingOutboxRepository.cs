using Microsoft.EntityFrameworkCore;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Infrastructure.Persistence;

namespace StudentCenter.BillingService.Infrastructure.Repositories;

public sealed class BillingOutboxRepository(BillingDbContext db) : IBillingOutboxRepository
{
    public async Task<IReadOnlyCollection<BillingOutboxMessage>> GetPendingAsync(CancellationToken ct)
    {
        return await db.OutboxMessages.Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.Sequence).Take(50).ToArrayAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }
}
