using StudentCenter.NotificationService.Application.DTOs;

namespace StudentCenter.NotificationService.Application.Interfaces;

public interface ICurrentNotificationOwner
{
    Task<NotificationOwner> GetAsync(CancellationToken ct);
}
