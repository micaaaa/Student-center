using Microsoft.EntityFrameworkCore;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.Infrastructure.Repositories;

public sealed class AccommodationOutboxRepository(AccommodationDbContext db) : IAccommodationOutboxRepository
{
    public async Task<IReadOnlyCollection<AccommodationOutboxMessage>> GetPendingAsync(CancellationToken ct) =>
        await db.OutboxMessages.Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.Sequence).Take(50).ToArrayAsync(ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
