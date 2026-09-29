using StudentCenter.AccommodationService.Application.DTOs;

namespace StudentCenter.AccommodationService.Application.Interfaces;

public interface IStudentAccommodationReader
{
    Task<MyAccommodationResponse?> GetCurrentAsync(Guid studentId, CancellationToken ct);
    Task<IReadOnlyCollection<MyAccommodationResponse>> GetHistoryAsync(Guid studentId, CancellationToken ct);
}
