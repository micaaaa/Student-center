using Microsoft.Extensions.Options;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.Messaging;

namespace StudentCenter.MaintenanceService.Infrastructure.Messaging;

public sealed class MaintenanceOutboxWorker(
    IServiceScopeFactory scopes, IOptions<RabbitOptions> options,
    TimeProvider clock, ILogger<MaintenanceOutboxWorker> logger) : BackgroundService
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
                await session.DeclareMaintenanceQueuesAsync(stoppingToken);
                var publisher = new MaintenanceEventPublisher(session.Channel);
                while (!stoppingToken.IsCancellationRequested)
                {
                    // A fresh context on every attempt avoids retaining failed publication flags in memory.
                    using var scope = scopes.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IMaintenanceOutboxRepository>();
                    await new MaintenanceOutboxDispatcher(repository, publisher, clock).DispatchAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Maintenance events remain in the outbox and will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}


