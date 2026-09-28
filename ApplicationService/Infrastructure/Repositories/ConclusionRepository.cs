using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Infrastructure.Repositories;

public sealed class ConclusionRepository : RankingRepository, IConclusionRepository
{
    private readonly ApplicationDbContext db;

    public ConclusionRepository(ApplicationDbContext db) : base(db)
    {
        this.db = db;
    }

    public Task<Ranking?> GetFinalAsync(Guid competitionId, CancellationToken ct) =>
        db.Rankings.Include(ranking => ranking.Entries).SingleOrDefaultAsync(
            ranking => ranking.CompetitionId == competitionId && ranking.Type == RankingType.Final, ct);

    public Task<StudentApplication?> GetApplicationAsync(Guid applicationId, CancellationToken ct) =>
        db.Applications.SingleOrDefaultAsync(application => application.Id == applicationId, ct);

    public async Task<IReadOnlyCollection<StudentApplication>> GetApplicationsAsync(Guid competitionId, CancellationToken ct) =>
        await db.Applications.Where(application => application.CompetitionId == competitionId).ToArrayAsync(ct);

    public async Task<IReadOnlyCollection<Appeal>> GetAppealsAsync(Guid competitionId, CancellationToken ct) =>
        await db.Appeals.Where(appeal => appeal.CompetitionId == competitionId)
            .OrderBy(appeal => appeal.SubmittedAtUtc).ThenBy(appeal => appeal.Id).ToArrayAsync(ct);

    public Task<Appeal?> GetAppealAsync(Guid appealId, CancellationToken ct) =>
        db.Appeals.SingleOrDefaultAsync(appeal => appeal.Id == appealId, ct);

    public async Task AddAppealAsync(Appeal appeal, CancellationToken ct)
    {
        await db.Appeals.AddAsync(appeal, ct);
    }

    public async Task AddDecisionAsync(AccommodationEligibility decision, CancellationToken ct)
    {
        await db.Eligibilities.AddAsync(decision, ct);
    }

    public async Task AddEventAsync(OutboxMessage message, CancellationToken ct)
    {
        await db.OutboxMessages.AddAsync(message, ct);
    }

    public async Task<IReadOnlyCollection<AccommodationEligibility>> GetDecisionsAsync(Guid competitionId, CancellationToken ct) =>
        await db.Eligibilities.AsNoTracking().Where(decision => decision.CompetitionId == competitionId)
            .OrderBy(decision => decision.StudentId).ToArrayAsync(ct);
}
