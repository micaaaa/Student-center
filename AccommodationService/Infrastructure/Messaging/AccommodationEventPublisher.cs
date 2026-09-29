using System.Text;
using RabbitMQ.Client;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.Messaging;

namespace StudentCenter.AccommodationService.Infrastructure.Messaging;

public sealed class AccommodationEventPublisher(IChannel channel) : IAccommodationEventPublisher
{
    public async Task PublishAsync(AccommodationOutboxMessage message, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await channel.BasicPublishAsync(
            RabbitSession.Exchange, message.Type, mandatory: true,
            basicProperties: new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Type = message.Type,
                MessageId = message.Id.ToString()
            },
            body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: timeout.Token);
    }
}
