using System.Text.Json;

namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid EligibilityId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public void MarkPublished(DateTime now)
    {
        PublishedAtUtc ??= now;
    }

    public static OutboxMessage EligibilityGranted(AccommodationEligibility decision)
    {
        if (!decision.Eligible)
            throw new ArgumentException("Only granted eligibility produces this event.");

        var eventId = Guid.NewGuid();
        return new OutboxMessage
        {
            Id = eventId,
            EligibilityId = decision.Id,
            Type = "AccommodationEligibilityGranted",
            OccurredAtUtc = decision.DecisionDateUtc,
            Payload = JsonSerializer.Serialize(new AccommodationEligibilityGranted(
                eventId, decision.Id, decision.StudentId, decision.CompetitionId,
                decision.AcademicYear, decision.DecisionDateUtc))
        };
    }
}

public sealed record AccommodationEligibilityGranted(
    Guid EventId, Guid EligibilityId, Guid StudentId, Guid CompetitionId,
    string AcademicYear, DateTime OccurredAtUtc);
