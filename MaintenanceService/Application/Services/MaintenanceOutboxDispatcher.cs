using StudentCenter.MaintenanceService.Application.Interfaces;

namespace StudentCenter.MaintenanceService.Application.Services;

public sealed class MaintenanceOutboxDispatcher(
    IMaintenanceOutboxRepository repository, IMaintenanceEventPublisher publisher, TimeProvider clock)
{
    public async Task DispatchAsync(CancellationToken ct)
    {
        foreach (var message in await repository.GetPendingAsync(ct))
        {
            await publisher.PublishAsync(message, ct);
            message.MarkPublished(clock.GetUtcNow().UtcDateTime);
            await repository.SaveAsync(ct);
        }
    }
}


