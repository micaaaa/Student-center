using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.Domain.Entities;

public sealed class MaintenanceRequest
{
    private MaintenanceRequest()
    {
    }

    public MaintenanceRequest(Guid studentId, Guid accommodationId, Guid roomId, MaintenanceCategory category,
        string title, string description, RequestPriority priority, DateTimeOffset now)
    {
        if (studentId == Guid.Empty || accommodationId == Guid.Empty || roomId == Guid.Empty)
        {
            throw new ArgumentException("Student, accommodation and room identifiers are required.");
        }

        if (!category.IsActive)
        {
            throw new MaintenanceConflictException("Inactive categories cannot be used for new requests.");
        }

        ValidatePriority(priority);
        Title = MaintenanceCategory.RequiredText(title, 200, "Title");
        Description = MaintenanceCategory.RequiredText(description, 4000, "Description");
        Id = Guid.NewGuid();
        StudentId = studentId;
        AccommodationId = accommodationId;
        RoomId = roomId;
        CategoryId = category.Id;
        Priority = priority;
        Status = RequestStatus.Submitted;
        CreatedAtUtc = now.UtcDateTime;
        UpdatedAtUtc = now.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid AccommodationId { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public RequestPriority Priority { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Accept(Guid actorId, DateTimeOffset now)
    {
        EnsureSubmitted();
        ValidateActor(actorId);
        Status = RequestStatus.Accepted;
        ReviewedByUserId = actorId;
        ReviewedAtUtc = now.UtcDateTime;
        UpdatedAtUtc = now.UtcDateTime;
    }

    public void Reject(Guid actorId, string reason, DateTimeOffset now)
    {
        EnsureSubmitted();
        ValidateActor(actorId);
        var cleanReason = MaintenanceCategory.RequiredText(reason, 2000, "Rejection reason");
        Status = RequestStatus.Rejected;
        RejectionReason = cleanReason;
        ReviewedByUserId = actorId;
        ReviewedAtUtc = now.UtcDateTime;
        UpdatedAtUtc = now.UtcDateTime;
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureSubmitted();
        Status = RequestStatus.Cancelled;
        CancelledAtUtc = now.UtcDateTime;
        UpdatedAtUtc = now.UtcDateTime;
    }

    public void ChangePriority(RequestPriority priority, DateTimeOffset now)
    {
        if (Status is RequestStatus.Rejected or RequestStatus.Cancelled)
        {
            throw new MaintenanceConflictException("Closed requests cannot be changed.");
        }

        ValidatePriority(priority);
        Priority = priority;
        UpdatedAtUtc = now.UtcDateTime;
    }

    private void EnsureSubmitted()
    {
        if (Status != RequestStatus.Submitted)
        {
            throw new MaintenanceConflictException("Only a submitted request can be accepted, rejected or cancelled.");
        }
    }

    private static void ValidateActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Staff identifier is required.");
        }
    }

    public static void ValidatePriority(RequestPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentException("Unknown request priority.");
        }
    }
}
