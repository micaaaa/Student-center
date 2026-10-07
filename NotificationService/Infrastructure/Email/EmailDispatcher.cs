using Microsoft.EntityFrameworkCore;
using StudentCenter.NotificationService.Domain.Entities;
using StudentCenter.NotificationService.Infrastructure.Persistence;

namespace StudentCenter.NotificationService.Infrastructure.Email;

public sealed class EmailDispatcher(
    NotificationDbContext db, IEmailRecipientClient recipients, IEmailSender sender,
    TimeProvider clock, ILogger<EmailDispatcher> logger)
{
    public async Task DispatchAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await db.EmailDeliveries.AsNoTracking()
            .Where(item => item.SentAtUtc == null && item.FailedAtUtc == null
                && item.NextAttemptAtUtc <= now && (item.LeaseUntilUtc == null || item.LeaseUntilUtc <= now))
            .OrderBy(item => item.NextAttemptAtUtc).Take(20).Select(item => item.Id).ToArrayAsync(ct);
        foreach (var id in candidates)
        {
            ct.ThrowIfCancellationRequested();
            now = clock.GetUtcNow().UtcDateTime;
            var lease = Guid.NewGuid();
            var claimed = await db.EmailDeliveries.Where(item => item.Id == id
                    && item.SentAtUtc == null && item.FailedAtUtc == null
                    && item.NextAttemptAtUtc <= now
                    && (item.LeaseUntilUtc == null || item.LeaseUntilUtc <= now))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.LeaseId, lease)
                    .SetProperty(item => item.LeaseUntilUtc, now.AddMinutes(2)), ct);
            if (claimed == 0)
            {
                continue;
            }

            var owned = db.EmailDeliveries.Where(item => item.Id == id && item.LeaseId == lease);
            var delivery = await owned.AsNoTracking().SingleAsync(ct);
            var notification = await db.Notifications.AsNoTracking().SingleAsync(item => item.Id == id, ct);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(45));
                var address = await recipients.GetAsync(notification, timeout.Token);
                await sender.SendAsync(notification, address, timeout.Token);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                var attempts = delivery.Attempts + 1;
                var failedAt = clock.GetUtcNow().UtcDateTime;
                // Store only the exception type; transport messages may contain private recipient data.
                var error = exception.GetType().Name;
                await owned.ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Attempts, attempts)
                    .SetProperty(item => item.LastError, error)
                    .SetProperty(item => item.FailedAtUtc, attempts >= 8 ? (DateTime?)failedAt : null)
                    .SetProperty(item => item.NextAttemptAtUtc, failedAt.Add(EmailDelivery.RetryDelay(attempts)))
                    .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(item => item.LeaseId, (Guid?)null), ct);
                logger.LogWarning("Email delivery {DeliveryId} failed on attempt {Attempt}: {ErrorType}",
                    id, attempts, error);
                continue;
            }

            await owned.ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Attempts, delivery.Attempts + 1)
                .SetProperty(item => item.SentAtUtc, clock.GetUtcNow().UtcDateTime)
                .SetProperty(item => item.LastError, (string?)null)
                .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(item => item.LeaseId, (Guid?)null), ct);
        }
    }
}
