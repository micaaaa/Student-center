using StudentCenter.AccommodationService.Application.Interfaces;

namespace StudentCenter.AccommodationService.Application.Services;

public sealed class AccommodationOutboxDispatcher(
    IAccommodationOutboxRepository repository, IAccommodationEventPublisher publisher, TimeProvider clock)
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
