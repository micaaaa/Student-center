namespace StudentCenter.AccommodationService.Domain.Entities;

public sealed class MoveIn
{
    private MoveIn()
    {
    }

    internal MoveIn(Guid accommodationId, DateTime dateUtc, string certificateReference, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        AccommodationId = accommodationId;
        DateUtc = dateUtc;
        MedicalCertificateReference = certificateReference;
        RecordedBy = recordedBy;
    }

    public Guid Id { get; private set; }
    public Guid AccommodationId { get; private set; }
    public DateTime DateUtc { get; private set; }
    public string MedicalCertificateReference { get; private set; } = null!;
    public Guid RecordedBy { get; private set; }
}
