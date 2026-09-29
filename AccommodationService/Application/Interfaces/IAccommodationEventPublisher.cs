using StudentCenter.AccommodationService.Domain.Entities;

namespace StudentCenter.AccommodationService.Application.Interfaces;

public interface IAccommodationEventPublisher
{
    // Completion means the broker confirmed the message and it was not returned as unroutable.
    Task PublishAsync(AccommodationOutboxMessage message, CancellationToken ct);
}
