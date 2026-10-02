using System.Text;
using RabbitMQ.Client;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.Messaging;

namespace StudentCenter.MaintenanceService.Infrastructure.Messaging;

public sealed class MaintenanceEventPublisher(IChannel channel) : IMaintenanceEventPublisher
{
    public async Task PublishAsync(MaintenanceOutboxMessage message, CancellationToken ct)
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


