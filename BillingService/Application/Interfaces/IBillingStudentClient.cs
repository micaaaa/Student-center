namespace StudentCenter.BillingService.Application.Interfaces;

public interface IBillingStudentClient
{
    Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct);
    Task EnsureExistsAsync(Guid studentId, CancellationToken ct);
}
