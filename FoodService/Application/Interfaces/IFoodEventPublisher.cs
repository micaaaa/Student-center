using StudentCenter.FoodService.Domain.Entities;

namespace StudentCenter.FoodService.Application.Interfaces;

public interface IFoodEventPublisher
{
    // Completion means the broker confirmed the message and it was not returned as unroutable.
    Task PublishAsync(FoodOutboxMessage message, CancellationToken ct);
}

