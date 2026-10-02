namespace StudentCenter.NotificationService.Domain.Entities;

public sealed class Notification
{
    private Notification()
    {
    }

    public Notification(Guid eventId, string eventType, string recipientKind, Guid recipientId,
        string title, string message, string resourceType, Guid resourceId, DateTime occurredAtUtc)
    {
        if (eventId == Guid.Empty || recipientId == Guid.Empty || resourceId == Guid.Empty
            || recipientKind is not ("STUDENT" or "USER") || occurredAtUtc == default
            || occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Valid event, recipient, resource and UTC timestamp are required.");
        }

        Id = Guid.NewGuid();
        EventId = eventId;
        EventType = Required(eventType, 100);
        RecipientKind = recipientKind;
        RecipientId = recipientId;
        Title = Required(title, 200);
        Message = Required(message, 1000);
        ResourceType = Required(resourceType, 30);
        ResourceId = resourceId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string RecipientKind { get; private set; } = null!;
    public Guid RecipientId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public string ResourceType { get; private set; } = null!;
    public Guid ResourceId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
        {
            throw new ArgumentException("Notification text is missing or too long.");
        }

        return value.Trim();
    }
}
