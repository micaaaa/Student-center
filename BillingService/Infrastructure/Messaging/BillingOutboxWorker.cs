using Microsoft.Extensions.Options;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Application.Services;
using StudentCenter.Messaging;

namespace StudentCenter.BillingService.Infrastructure.Messaging;

public sealed class BillingOutboxWorker(
    IServiceScopeFactory scopes, IOptions<RabbitOptions> options,
    TimeProvider clock, ILogger<BillingOutboxWorker> logger) : BackgroundService
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
                await session.DeclareBillingQueuesAsync(stoppingToken);
                var publisher = new BillingEventPublisher(session.Channel);
                while (!stoppingToken.IsCancellationRequested)
                {
                    // A fresh context on every attempt avoids retaining failed publication flags in memory.
                    using var scope = scopes.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IBillingOutboxRepository>();
                    await new BillingOutboxDispatcher(repository, publisher, clock).DispatchAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Billing events remain in the outbox and will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
