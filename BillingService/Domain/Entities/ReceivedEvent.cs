namespace StudentCenter.BillingService.Domain.Entities;

public sealed class ReceivedEvent
{
    private ReceivedEvent()
    {
    }

    public ReceivedEvent(Guid id, string fingerprint, DateTime receivedAtUtc)
    {
        Id = id;
        Fingerprint = fingerprint;
        ReceivedAtUtc = receivedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Fingerprint { get; private set; } = null!;
    public DateTime ReceivedAtUtc { get; private set; }
}

