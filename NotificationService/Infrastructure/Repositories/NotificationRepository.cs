using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.NotificationService.Application.DTOs;
using StudentCenter.NotificationService.Application.Interfaces;
using StudentCenter.NotificationService.Domain.Entities;
using StudentCenter.NotificationService.Infrastructure.Persistence;

namespace StudentCenter.NotificationService.Infrastructure.Repositories;

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task ReceiveAsync(PreparedEvent message, DateTime receivedAtUtc, CancellationToken ct)
    {
        var existing = await db.ReceivedEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == message.Id, ct);
        if (existing is not null)
        {
            EnsureSameEvent(existing, message);
            return;
        }

        db.ReceivedEvents.Add(new ReceivedEvent(message.Id, message.Fingerprint, receivedAtUtc));
        db.Notifications.AddRange(message.Notifications);
        try
        {
            // EF commits the receipt and all notifications in a single SQL transaction.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Another consumer may have committed this event after the initial lookup.
            db.ChangeTracker.Clear();
            existing = await db.ReceivedEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == message.Id, ct);
            if (existing is null)
            {
                throw;
            }

            EnsureSameEvent(existing, message);
        }
    }

    public async Task<IReadOnlyCollection<Notification>> ListAsync(
        NotificationOwner owner, bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        return await Owned(owner).Where(item => !unreadOnly || item.ReadAtUtc == null)
            .OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    }

    public Task<int> CountUnreadAsync(NotificationOwner owner, CancellationToken ct)
    {
        return Owned(owner).CountAsync(item => item.ReadAtUtc == null, ct);
    }

    public Task<Notification?> GetAsync(Guid id, NotificationOwner owner, CancellationToken ct)
    {
        return Owned(owner).SingleOrDefaultAsync(item => item.Id == id, ct);
    }

    public async Task<bool> MarkReadAsync(Guid id, NotificationOwner owner, DateTime now, CancellationToken ct)
    {
        // Conditional update preserves the first read timestamp even with concurrent requests.
        var changed = await Owned(owner).Where(item => item.Id == id && item.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadAtUtc, now), ct);
        return changed > 0 || await Owned(owner).AnyAsync(item => item.Id == id, ct);
    }

    private IQueryable<Notification> Owned(NotificationOwner owner)
    {
        if (owner.UserId == Guid.Empty || owner.StudentId == Guid.Empty)
        {
            throw new ArgumentException("A valid notification owner is required.");
        }

        return db.Notifications.AsNoTracking().Where(item =>
            item.RecipientKind == "USER" && item.RecipientId == owner.UserId
            || item.RecipientKind == "STUDENT" && owner.StudentId.HasValue && item.RecipientId == owner.StudentId);
    }

    private static void EnsureSameEvent(ReceivedEvent existing, PreparedEvent message)
    {
        if (existing.Fingerprint != message.Fingerprint)
        {
            throw new ArgumentException("Event ID was reused with different content.");
        }
    }
}
