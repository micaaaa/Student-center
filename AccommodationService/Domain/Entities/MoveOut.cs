namespace StudentCenter.AccommodationService.Domain.Entities;

public sealed class MoveOut
{
    private MoveOut()
    {
    }

    internal MoveOut(Guid accommodationId, DateTime dateUtc, string reason, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        AccommodationId = accommodationId;
        DateUtc = dateUtc;
        Reason = reason;
        RecordedBy = recordedBy;
    }

    public Guid Id { get; private set; }
    public Guid AccommodationId { get; private set; }
    public DateTime DateUtc { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid RecordedBy { get; private set; }
}
