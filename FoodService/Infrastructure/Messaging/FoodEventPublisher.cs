using System.Text;
using RabbitMQ.Client;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.Messaging;

namespace StudentCenter.FoodService.Infrastructure.Messaging;

public sealed class FoodEventPublisher(IChannel channel) : IFoodEventPublisher
{
    public async Task PublishAsync(FoodOutboxMessage message, CancellationToken ct)
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

