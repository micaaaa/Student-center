using System.Text.Json;
using Microsoft.Extensions.Options;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Services;
using StudentCenter.Messaging;

namespace StudentCenter.AccommodationService.Infrastructure.Messaging;

public sealed class EligibilityConsumer(
    IServiceScopeFactory scopes,
    IOptions<RabbitOptions> options,
    ILogger<EligibilityConsumer> logger) : BackgroundService
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
                    var delivery = await session.Channel.BasicGetAsync(
                        RabbitSession.EligibilityQueue, autoAck: false, stoppingToken);
                    if (delivery is null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        continue;
                    }

                    try
                    {
                        var message = JsonSerializer.Deserialize<EligibilityGrantedEvent>(delivery.Body.Span)
                            ?? throw new JsonException("Eligibility event body is missing.");
                        if (delivery.BasicProperties.Type != RabbitSession.EligibilityEvent
                            || delivery.BasicProperties.MessageId != message.EventId.ToString())
                            throw new ArgumentException("Eligibility event metadata does not match its body.");

                        using var scope = scopes.CreateScope();
                        var service = scope.ServiceProvider.GetRequiredService<AssignmentService>();
                        await service.ReceiveEligibilityAsync(message, stoppingToken);
                    }
                    catch (Exception exception) when (exception is JsonException or ArgumentException)
                    {
                        logger.LogWarning("Rejected invalid eligibility event {MessageId}: {Error}",
                            delivery.BasicProperties.MessageId, exception.Message);
                        await session.Channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, stoppingToken);
                        continue;
                    }

                    // SQL commit comes first. If ACK fails, the next delivery is deduplicated.
                    await session.Channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Disposing the channel returns unacknowledged messages to the queue.
                logger.LogWarning(exception, "Eligibility consumption failed; reconnecting before retry.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
