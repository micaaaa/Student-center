using StudentCenter.MaintenanceService.Domain.Entities;

namespace StudentCenter.MaintenanceService.Application.Interfaces;

public interface IMaintenanceOutboxRepository
{
    Task<IReadOnlyCollection<MaintenanceOutboxMessage>> GetPendingAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}


