namespace StudentCenter.FoodService.Application.Interfaces;

public interface IFoodStudentClient
{
    Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct);
    Task EnsureActiveStudentAsync(Guid studentId, CancellationToken ct);
}
