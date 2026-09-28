using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Infrastructure.Repositories;

public class RankingRepository(ApplicationDbContext db) : IRankingRepository
{
    public Task<Competition?> GetCompetitionAsync(Guid competitionId, CancellationToken ct) =>
        db.Competitions.SingleOrDefaultAsync(competition => competition.Id == competitionId, ct);

    public Task<Ranking?> GetPreliminaryAsync(Guid competitionId, CancellationToken ct) =>
        db.Rankings.Include(ranking => ranking.Entries).SingleOrDefaultAsync(
            ranking => ranking.CompetitionId == competitionId && ranking.Type == RankingType.Preliminary, ct);

    public async Task<IReadOnlyCollection<RankingCandidate>> GetCandidatesAsync(
        Guid competitionId, CancellationToken ct)
    {
        var applications = await db.Applications.AsNoTracking()
            .Where(application => application.CompetitionId == competitionId).ToArrayAsync(ct);
        var scores = await (
            from score in db.Scores.AsNoTracking()
            join application in db.Applications on score.ApplicationId equals application.Id
            where application.CompetitionId == competitionId
            select score).ToDictionaryAsync(score => score.ApplicationId, ct);
        var documents = await (
            from document in db.Documents.AsNoTracking()
            join application in db.Applications on document.ApplicationId equals application.Id
            where application.CompetitionId == competitionId
            select document).ToArrayAsync(ct);
        var byApplication = documents.ToLookup(document => document.ApplicationId);

        return applications.Select(application => new RankingCandidate(
            application, scores.GetValueOrDefault(application.Id), byApplication[application.Id].ToArray())).ToArray();
    }

    public async Task AddAsync(Ranking ranking, CancellationToken ct)
    {
        await db.Rankings.AddAsync(ranking, ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

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
            throw new ApplicationConflictException("The ranking has changed. Reload it before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        {
            throw new ApplicationConflictException("Ranking data changed concurrently. Reload it before retrying.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new ApplicationConflictException("Ranking data changed concurrently. Reload it before retrying.");
        }
    }
}
