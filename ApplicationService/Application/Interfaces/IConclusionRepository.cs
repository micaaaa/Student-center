using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Application.Interfaces;

public interface IConclusionRepository : IRankingRepository
{
    Task<Ranking?> GetFinalAsync(Guid competitionId, CancellationToken ct);

    Task<StudentApplication?> GetApplicationAsync(Guid applicationId, CancellationToken ct);

    Task<IReadOnlyCollection<StudentApplication>> GetApplicationsAsync(Guid competitionId, CancellationToken ct);

    Task<IReadOnlyCollection<Appeal>> GetAppealsAsync(Guid competitionId, CancellationToken ct);

    Task<Appeal?> GetAppealAsync(Guid appealId, CancellationToken ct);

    Task AddAppealAsync(Appeal appeal, CancellationToken ct);

    Task AddDecisionAsync(AccommodationEligibility decision, CancellationToken ct);

    Task AddEventAsync(OutboxMessage message, CancellationToken ct);

    Task<IReadOnlyCollection<AccommodationEligibility>> GetDecisionsAsync(Guid competitionId, CancellationToken ct);
}
