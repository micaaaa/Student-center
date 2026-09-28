using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Application.Interfaces;

public interface IScoringRepository
{
    Task<ScoringResult?> FindAsync(Guid applicationId, CancellationToken ct);

    Task AddAsync(ScoringResult score, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
