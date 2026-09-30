using Microsoft.EntityFrameworkCore;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Infrastructure.Persistence;

namespace StudentCenter.FoodService.Infrastructure.Repositories;

public sealed class FoodOutboxRepository(FoodDbContext db) : IFoodOutboxRepository
{
    public async Task<IReadOnlyCollection<FoodOutboxMessage>> GetPendingAsync(CancellationToken ct)
    {
        return await db.OutboxMessages.Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.Sequence).Take(50).ToArrayAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }
}

