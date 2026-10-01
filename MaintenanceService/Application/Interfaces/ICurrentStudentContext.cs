using StudentCenter.MaintenanceService.Application.DTOs;

namespace StudentCenter.MaintenanceService.Application.Interfaces;

public interface ICurrentStudentContext
{
    Task<Guid> GetStudentIdAsync(CancellationToken ct);
    Task<CurrentAccommodation> GetActiveAccommodationAsync(CancellationToken ct);
}
