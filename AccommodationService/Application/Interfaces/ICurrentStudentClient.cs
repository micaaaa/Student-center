namespace StudentCenter.AccommodationService.Application.Interfaces;

public interface ICurrentStudentClient
{
    Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct);
}
