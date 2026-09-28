using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Application.Interfaces;

public sealed record RankingCandidate(
    StudentApplication Application, ScoringResult? Score, IReadOnlyCollection<ApplicationDocument> Documents);

public interface IRankingRepository
{
    Task<Competition?> GetCompetitionAsync(Guid competitionId, CancellationToken ct);

    Task<Ranking?> GetPreliminaryAsync(Guid competitionId, CancellationToken ct);

    Task<IReadOnlyCollection<RankingCandidate>> GetCandidatesAsync(Guid competitionId, CancellationToken ct);

    Task AddAsync(Ranking ranking, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);

    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
