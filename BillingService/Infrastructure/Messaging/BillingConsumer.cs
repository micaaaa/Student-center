using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StudentCenter.Messaging;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Application.Services;

namespace StudentCenter.BillingService.Infrastructure.Messaging;

public sealed class BillingConsumer(
    IServiceScopeFactory scopes, IOptions<RabbitOptions> options,
    TimeProvider clock, ILogger<BillingConsumer> logger) : BackgroundService
{
    public const string RejectedQueue = "billing.rejected";
    private static readonly string[] Queues =
    [
        "billing.accommodation", "billing.food"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var session = await RabbitSession.OpenAsync(options.Value, stoppingToken);
                await session.DeclareAccommodationQueuesAsync(stoppingToken);
                await session.DeclareFoodQueuesAsync(stoppingToken);

                await session.Channel.QueueDeclareAsync(RejectedQueue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var receivedAny = false;
                    foreach (var queue in Queues)
                    {
                        var delivery = await session.Channel.BasicGetAsync(queue, autoAck: false, stoppingToken);
                        if (delivery is null)
                        {
                            continue;
                        }

                        receivedAny = true;
                        try
                        {
                            var message = BillingEventParser.Parse(queue, delivery.BasicProperties.Type,
                                delivery.BasicProperties.MessageId, delivery.Body);
                            using var scope = scopes.CreateScope();
                            var repository = scope.ServiceProvider.GetRequiredService<IBillingRepository>();
                            await repository.ReceiveAsync(message, clock.GetUtcNow(), stoppingToken);
                        }
                        catch (Exception exception) when (exception is JsonException or ArgumentException)
                        {
                            // Existing durable queues have no DLX. Quarantine with publisher confirmation before ACK.
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                            timeout.CancelAfter(TimeSpan.FromSeconds(15));
                            await session.Channel.BasicPublishAsync("", RejectedQueue, mandatory: true,
                                basicProperties: new BasicProperties
                                {
                                    Persistent = true,
                                    ContentType = "application/json",
                                    Type = delivery.BasicProperties.Type,
                                    MessageId = delivery.BasicProperties.MessageId,
                                    Headers = new Dictionary<string, object?> { ["source-queue"] = queue }
                                }, body: delivery.Body, cancellationToken: timeout.Token);
                            logger.LogWarning("An invalid event from {Queue} was moved to {RejectedQueue}.", queue, RejectedQueue);
                        }

                        // ACK only after SQL commit or confirmed quarantine. Redelivery is deduplicated by EventId.
                        await session.Channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                    }

                    if (!receivedAny)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Channel disposal requeues unacknowledged events after transient SQL or broker failures.
                logger.LogWarning(exception, "Billing consumption failed; reconnecting before retry.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}


