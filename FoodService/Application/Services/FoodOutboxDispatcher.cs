using StudentCenter.FoodService.Application.Interfaces;

namespace StudentCenter.FoodService.Application.Services;

public sealed class FoodOutboxDispatcher(
    IFoodOutboxRepository repository, IFoodEventPublisher publisher, TimeProvider clock)
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

