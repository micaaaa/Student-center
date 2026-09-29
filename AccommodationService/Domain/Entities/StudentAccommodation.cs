using StudentCenter.AccommodationService.Domain.Exceptions;

namespace StudentCenter.AccommodationService.Domain.Entities;

public sealed class StudentAccommodation
{
    private StudentAccommodation()
    {
    }

    public StudentAccommodation(ReceivedEligibility eligibility, Guid roomId, Guid assignedBy, DateTime now)
    {
        if (roomId == Guid.Empty || assignedBy == Guid.Empty)
            throw new ArgumentException("Room and staff identifiers are required.");

        Id = Guid.NewGuid();
        EligibilityId = eligibility.Id;
        StudentId = eligibility.StudentId;
        AcademicYear = eligibility.AcademicYear;
        RoomId = roomId;
        AssignedBy = assignedBy;
        AssignedAtUtc = now;
        IsActive = true;
        Status = "ASSIGNED";
    }

    public Guid Id { get; private set; }
    public Guid EligibilityId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid RoomId { get; private set; }
    public string AcademicYear { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public MoveIn? MoveIn { get; private set; }
    public MoveOut? MoveOut { get; private set; }

    public void RecordMoveIn(Guid staffId, string certificateReference, DateTime now)
    {
        if (!IsActive || Status != "ASSIGNED" || MoveIn is not null)
            throw new AccommodationConflictException("Only an assigned accommodation can be moved into once.");
        if (staffId == Guid.Empty || string.IsNullOrWhiteSpace(certificateReference)
            || certificateReference.Trim().Length > 250)
            throw new ArgumentException("Staff identifier and a medical certificate reference up to 250 characters are required.");
        if (now.Kind != DateTimeKind.Utc || now < AssignedAtUtc)
            throw new ArgumentException("Move-in time must be UTC and cannot precede assignment.");

        MoveIn = new MoveIn(Id, now, certificateReference.Trim(), staffId);
        Status = "ACTIVE";
    }

    public void RecordMoveOut(Guid staffId, string reason, DateTime now)
    {
        if (!IsActive || Status != "ACTIVE" || MoveIn is null || MoveOut is not null)
            throw new AccommodationConflictException("Only a moved-in accommodation can be moved out of once.");
        if (staffId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new ArgumentException("Staff identifier and a move-out reason up to 1000 characters are required.");
        if (now.Kind != DateTimeKind.Utc || now < MoveIn.DateUtc)
            throw new ArgumentException("Move-out time must be UTC and cannot precede move-in.");

        MoveOut = new MoveOut(Id, now, reason.Trim(), staffId);
        Status = "COMPLETED";
        IsActive = false;
    }

    public void Cancel(Guid staffId, string reason, DateTime now)
    {
        if (!IsActive || Status != "ASSIGNED")
            throw new AccommodationConflictException("Only an assigned accommodation can be cancelled.");
        if (staffId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new ArgumentException("Staff identifier and a cancellation reason up to 1000 characters are required.");
        if (now < AssignedAtUtc)
            throw new ArgumentException("Cancellation cannot precede assignment.");

        Status = "CANCELLED";
        IsActive = false;
        CancelledBy = staffId;
        CancelledAtUtc = now;
        CancellationReason = reason.Trim();
    }
}
