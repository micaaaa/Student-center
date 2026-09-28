using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Services;
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
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var changedScores = db.ChangeTracker.Entries<ScoringResult>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry => entry.Entity).ToArray();
            foreach (var score in changedScores)
            {
                var application = await db.Applications.AsNoTracking()
                    .SingleAsync(item => item.Id == score.ApplicationId, ct);
                var documents = await db.Documents.AsNoTracking()
                    .Where(item => item.ApplicationId == score.ApplicationId).ToArrayAsync(ct);
                if (application.Status != ApplicationStatus.UnderReview
                    || !DocumentReviewSnapshot.AllValid(documents)
                    || score.DocumentReviewFingerprint != DocumentReviewSnapshot.Fingerprint(documents))
                    throw new ApplicationConflictException("The application or its documents changed. Reload before scoring.");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApplicationConflictException("The score has changed. Reload it before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        {
            throw new ApplicationConflictException("A score was saved concurrently. Reload it before retrying.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new ApplicationConflictException("Scoring conflicted with another operation. Reload before retrying.");
        }
    }
}
