using StudentCenter.AccommodationService.Domain.Entities;

namespace StudentCenter.AccommodationService.Application.Interfaces;

public interface IAssignmentRepository
{
    Task<ReceivedEligibility?> GetEligibilityAsync(Guid id, CancellationToken ct);
    Task<ReceivedEligibility?> GetByEventAsync(Guid eventId, CancellationToken ct);
    Task<IReadOnlyCollection<ReceivedEligibility>> GetEligibilitiesAsync(Guid competitionId, CancellationToken ct);
    Task AddEligibilityAsync(ReceivedEligibility eligibility, CancellationToken ct);
    Task<bool> HasActiveAssignmentAsync(Guid studentId, CancellationToken ct);
    Task<StudentAccommodation?> GetAssignmentAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<StudentAccommodation>> GetHistoryAsync(Guid studentId, CancellationToken ct);
    Task AddAssignmentAsync(StudentAccommodation assignment, CancellationToken ct);
    Task AddEventAsync(AccommodationOutboxMessage message, CancellationToken ct);
}
