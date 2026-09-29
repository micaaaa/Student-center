using System.Text.Json;

namespace StudentCenter.AccommodationService.Domain.Entities;

public sealed class AccommodationOutboxMessage
{
    private AccommodationOutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public Guid AccommodationId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public static AccommodationOutboxMessage From(StudentAccommodation accommodation)
    {
        var (type, occurredAt) = accommodation.Status switch
        {
            "ASSIGNED" => ("AccommodationAssigned", accommodation.AssignedAtUtc),
            "ACTIVE" when accommodation.MoveIn is not null => ("StudentMovedIn", accommodation.MoveIn.DateUtc),
            "COMPLETED" when accommodation.MoveOut is not null => ("StudentMovedOut", accommodation.MoveOut.DateUtc),
            "CANCELLED" when accommodation.CancelledAtUtc.HasValue =>
                ("AccommodationAssignmentCancelled", accommodation.CancelledAtUtc.Value),
            _ => throw new ArgumentException("The accommodation has no supported lifecycle event.")
        };
        var id = Guid.NewGuid();
        var timestamp = DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc);
        return new AccommodationOutboxMessage
        {
            Id = id,
            AccommodationId = accommodation.Id,
            Type = type,
            OccurredAtUtc = timestamp,
            Payload = JsonSerializer.Serialize(new AccommodationLifecycleEvent(
                id, accommodation.Id, accommodation.StudentId, accommodation.RoomId,
                accommodation.AcademicYear, type, timestamp))
        };
    }

    public void MarkPublished(DateTime now)
    {
        PublishedAtUtc ??= now;
    }
}

public sealed record AccommodationLifecycleEvent(
    Guid EventId, Guid AccommodationId, Guid StudentId, Guid RoomId,
    string AcademicYear, string Type, DateTime OccurredAtUtc);
