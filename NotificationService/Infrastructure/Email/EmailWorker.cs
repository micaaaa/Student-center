using Microsoft.Extensions.Options;

namespace StudentCenter.NotificationService.Infrastructure.Email;

public sealed class EmailWorker(
    IServiceScopeFactory scopes, IOptions<EmailOptions> options, ILogger<EmailWorker> logger) : BackgroundService
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
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Email queue processing failed: {ErrorType}", exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
