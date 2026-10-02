using StudentCenter.MaintenanceService.Domain.Entities;

namespace StudentCenter.MaintenanceService.Application.Interfaces;

public interface IMaintenanceEventPublisher
{
    // Completion means the broker confirmed the message and it was not returned as unroutable.
    Task PublishAsync(MaintenanceOutboxMessage message, CancellationToken ct);
}


