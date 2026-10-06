using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;

namespace StudentCenter.ApplicationService.Application.Interfaces;

public interface IApplicationReviewRepository
{
    Task<IReadOnlyCollection<StudentApplication>> ListAsync(
        Guid? competitionId, ApplicationStatus? status, int page, CancellationToken ct, Guid? studentId = null);

    Task SaveAsync(CancellationToken ct);
}
