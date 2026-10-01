namespace StudentCenter.MaintenanceService.Application.Interfaces;

public interface IStaffDirectoryClient
{
    Task EnsureActiveStaffAsync(Guid userId, CancellationToken ct);
}
