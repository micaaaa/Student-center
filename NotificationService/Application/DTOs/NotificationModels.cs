using StudentCenter.NotificationService.Domain.Entities;

namespace StudentCenter.NotificationService.Application.DTOs;

// Constructed from authenticated claims and StudentService /me, never from request parameters.
public sealed record NotificationOwner(Guid UserId, Guid? StudentId);

public sealed record NotificationResponse(Guid Id, string EventType, string Title, string Message,
    string ResourceType, Guid ResourceId, DateTime OccurredAtUtc, DateTime? ReadAtUtc)
{
    public static NotificationResponse From(Notification notification)
    {
        return new NotificationResponse(notification.Id, notification.EventType, notification.Title,
            notification.Message, notification.ResourceType, notification.ResourceId,
            DateTime.SpecifyKind(notification.OccurredAtUtc, DateTimeKind.Utc),
            notification.ReadAtUtc.HasValue
                ? DateTime.SpecifyKind(notification.ReadAtUtc.Value, DateTimeKind.Utc)
                : null);
    }
}

public sealed record PreparedEvent(Guid Id, string Fingerprint, IReadOnlyCollection<Notification> Notifications);
