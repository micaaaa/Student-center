namespace StudentCenter.NotificationService.Domain.Entities;

public sealed class EmailDelivery
{
    private EmailDelivery()
    {
    }

    public EmailDelivery(Guid notificationId, DateTime createdAtUtc)
    {
        Id = notificationId;
        NextAttemptAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? LeaseUntilUtc { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public static bool ShouldSend(string eventType) => eventType is
        "FinalRankingPublished" or "AccommodationAssigned" or "AccommodationAssignmentCancelled"
        or "ChargeCreated" or "MaintenanceWorkerAssigned" or "MaintenanceRequestResolved";

    public static TimeSpan RetryDelay(int attempts) =>
        TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Clamp(attempts - 1, 0, 6))));
}
