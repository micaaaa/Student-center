using StudentCenter.BillingService.Application.Interfaces;

namespace StudentCenter.BillingService.Application.Services;

public sealed class BillingOutboxDispatcher(
    IBillingOutboxRepository repository, IBillingEventPublisher publisher, TimeProvider clock)
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
