using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Infrastructure.Repositories;

public sealed class ScoringRepository(ApplicationDbContext db) : IScoringRepository
{
    public Task<ScoringResult?> FindAsync(Guid applicationId, CancellationToken ct) =>
        db.Scores.SingleOrDefaultAsync(score => score.ApplicationId == applicationId, ct);

    public async Task AddAsync(ScoringResult score, CancellationToken ct)
    {
        await db.Scores.AddAsync(score, ct);
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApplicationConflictException("The score has changed. Reload it before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ApplicationConflictException("A score was saved concurrently. Reload it before retrying.");
        }
    }
}
