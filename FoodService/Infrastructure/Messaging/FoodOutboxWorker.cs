using Microsoft.Extensions.Options;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Application.Services;
using StudentCenter.Messaging;

namespace StudentCenter.FoodService.Infrastructure.Messaging;

public sealed class FoodOutboxWorker(
    IServiceScopeFactory scopes, IOptions<RabbitOptions> options,
    TimeProvider clock, ILogger<FoodOutboxWorker> logger) : BackgroundService
{
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
                await session.DeclareFoodQueuesAsync(stoppingToken);
                var publisher = new FoodEventPublisher(session.Channel);
                while (!stoppingToken.IsCancellationRequested)
                {
                    // A fresh context on every attempt avoids retaining failed publication flags in memory.
                    using var scope = scopes.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IFoodOutboxRepository>();
                    await new FoodOutboxDispatcher(repository, publisher, clock).DispatchAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Food events remain in the outbox and will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

