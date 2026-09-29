using Microsoft.Extensions.Options;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Application.Services;
using StudentCenter.Messaging;

namespace StudentCenter.AccommodationService.Infrastructure.Messaging;

public sealed class AccommodationOutboxWorker(
    IServiceScopeFactory scopes, IOptions<RabbitOptions> options,
    TimeProvider clock, ILogger<AccommodationOutboxWorker> logger) : BackgroundService
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
                await session.DeclareAccommodationQueuesAsync(stoppingToken);
                var publisher = new AccommodationEventPublisher(session.Channel);
                while (!stoppingToken.IsCancellationRequested)
                {
                    // A fresh context on every attempt avoids retaining failed publication flags in memory.
                    using var scope = scopes.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IAccommodationOutboxRepository>();
                    await new AccommodationOutboxDispatcher(repository, publisher, clock).DispatchAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Accommodation events remain in the outbox and will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
