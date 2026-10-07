using System.Text.Json;

namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid? EligibilityId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public void MarkPublished(DateTime now)
    {
        PublishedAtUtc ??= now;
    }

    public static OutboxMessage ApplicationNotification(string type, Guid applicationId, Guid studentId, Guid competitionId, DateTime occurredAtUtc)
    {
        if (type is not ("ApplicationSubmitted" or "PreliminaryRankingPublished" or "FinalRankingPublished"))
            throw new ArgumentException("Unsupported application notification event.");
        var identity = System.Text.Encoding.UTF8.GetBytes($"{type}:{applicationId}");
        var eventId = new Guid(System.Security.Cryptography.SHA256.HashData(identity).AsSpan(0, 16));
        return new OutboxMessage
        {
            Id = eventId,
            Type = type,
            OccurredAtUtc = occurredAtUtc,
            Payload = JsonSerializer.Serialize(new
            {
                EventId = eventId, Type = type, ApplicationId = applicationId,
                StudentId = studentId, CompetitionId = competitionId, OccurredAtUtc = occurredAtUtc
            })
        };
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
