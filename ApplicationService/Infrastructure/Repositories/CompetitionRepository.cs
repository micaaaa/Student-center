using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Infrastructure.Repositories;

public sealed class CompetitionRepository(ApplicationDbContext db) : ICompetitionRepository
{
    public Task<Competition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Competitions.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyCollection<Competition>> GetAllAsync(CancellationToken ct = default) =>
        await db.Competitions.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    public async Task AddAsync(Competition c, CancellationToken ct = default)
    {
        await db.Competitions.AddAsync(c, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
