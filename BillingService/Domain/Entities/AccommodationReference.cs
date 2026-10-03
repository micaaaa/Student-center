namespace StudentCenter.BillingService.Domain.Entities;

// Billing keeps only the references and lifecycle needed to verify a manually entered housing charge.
public sealed class AccommodationReference
{
    private AccommodationReference()
    {
    }

    public AccommodationReference(Guid id, Guid studentId, Guid roomId, string academicYear, string eventType)
    {
        Id = id;
        StudentId = studentId;
        RoomId = roomId;
        AcademicYear = academicYear;
        Apply(eventType);
    }

    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid RoomId { get; private set; }
    public string AcademicYear { get; private set; } = null!;
    public int Stage { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Apply(string eventType)
    {
        var stage = eventType switch
        {
            "AccommodationAssigned" => 1,
            "StudentMovedIn" => 2,
            "StudentMovedOut" => 3,
            "AccommodationAssignmentCancelled" => 4,
            _ => throw new ArgumentException("Unknown accommodation event.")
        };
        Stage = Math.Max(Stage, stage);
    }
}
