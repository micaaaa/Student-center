using System.Text;
using RabbitMQ.Client;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.Messaging;

namespace StudentCenter.BillingService.Infrastructure.Messaging;

public sealed class BillingEventPublisher(IChannel channel) : IBillingEventPublisher
{
    public async Task PublishAsync(BillingOutboxMessage message, CancellationToken ct)
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
