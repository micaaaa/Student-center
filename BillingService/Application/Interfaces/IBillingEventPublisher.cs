using StudentCenter.BillingService.Domain.Entities;

namespace StudentCenter.BillingService.Application.Interfaces;

public interface IBillingEventPublisher
{
    // Completion means the broker confirmed the message and it was not returned as unroutable.
    Task PublishAsync(BillingOutboxMessage message, CancellationToken ct);
}
