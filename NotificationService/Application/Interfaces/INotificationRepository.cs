using StudentCenter.NotificationService.Application.DTOs;
using StudentCenter.NotificationService.Domain.Entities;

namespace StudentCenter.NotificationService.Application.Interfaces;

public interface INotificationRepository
{
    Task ReceiveAsync(PreparedEvent message, DateTime receivedAtUtc, CancellationToken ct);
    Task<IReadOnlyCollection<Notification>> ListAsync(
        NotificationOwner owner, bool unreadOnly, int page, int pageSize, CancellationToken ct);
    Task<int> CountUnreadAsync(NotificationOwner owner, CancellationToken ct);
    Task<Notification?> GetAsync(Guid id, NotificationOwner owner, CancellationToken ct);
    Task<bool> MarkReadAsync(Guid id, NotificationOwner owner, DateTime now, CancellationToken ct);
}
