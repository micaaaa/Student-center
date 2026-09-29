using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StudentCenter.ApplicationService.Infrastructure.Persistence;
using StudentCenter.Messaging;

namespace StudentCenter.ApplicationService.Infrastructure.Messaging;

public sealed class EligibilityOutboxPublisher(
    IServiceScopeFactory scopes,
    IOptions<RabbitOptions> options,
    ILogger<EligibilityOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var session = await RabbitSession.OpenAsync(options.Value, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var messages = await db.OutboxMessages
                        .Where(message => message.PublishedAtUtc == null
                            && message.Type == RabbitSession.EligibilityEvent)
                        .OrderBy(message => message.OccurredAtUtc)
                        .ThenBy(message => message.Id)
                        .Take(50)
                        .ToArrayAsync(stoppingToken);

                    foreach (var message in messages)
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(15));
                        await session.Channel.BasicPublishAsync(
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

                        // Only mark after broker confirmation. A crash before saving causes safe redelivery.
                        message.MarkPublished(DateTime.UtcNow);
                        await db.SaveChangesAsync(stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Eligibility publication failed; pending outbox messages will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
