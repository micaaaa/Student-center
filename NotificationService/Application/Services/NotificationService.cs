using StudentCenter.NotificationService.Application.DTOs;
using StudentCenter.NotificationService.Application.Interfaces;

namespace StudentCenter.NotificationService.Application.Services;

public sealed class NotificationService(
    INotificationRepository repository, ICurrentNotificationOwner currentOwner, TimeProvider clock)
{
    public async Task<IReadOnlyCollection<NotificationResponse>> ListAsync(
        bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        }

        var owner = await currentOwner.GetAsync(ct);
        return (await repository.ListAsync(owner, unreadOnly, page, pageSize, ct))
            .Select(NotificationResponse.From).ToArray();
    }

    public async Task<int> CountUnreadAsync(CancellationToken ct)
    {
        return await repository.CountUnreadAsync(await currentOwner.GetAsync(ct), ct);
    }

    public async Task<NotificationResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var notification = await repository.GetAsync(id, await currentOwner.GetAsync(ct), ct)
            ?? throw new KeyNotFoundException("Notification not found.");
        return NotificationResponse.From(notification);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        if (!await repository.MarkReadAsync(id, await currentOwner.GetAsync(ct), clock.GetUtcNow().UtcDateTime, ct))
        {
            throw new KeyNotFoundException("Notification not found.");
        }
    }
}
