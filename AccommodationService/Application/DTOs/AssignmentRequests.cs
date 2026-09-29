using System.ComponentModel.DataAnnotations;

namespace StudentCenter.AccommodationService.Application.DTOs;

public sealed record AssignRoomRequest(Guid EligibilityId, Guid RoomId);

public sealed class CancelAssignmentRequest
{
    [Required, MaxLength(1000)]
    public string Reason { get; init; } = "";
}

public sealed record EligibilityGrantedEvent(
    Guid EventId, Guid EligibilityId, Guid StudentId, Guid CompetitionId,
    string AcademicYear, DateTime OccurredAtUtc);

public sealed record ReceivedEligibilityResponse(
    Guid Id, Guid StudentId, Guid CompetitionId, string AcademicYear, DateTime GrantedAtUtc);

public sealed record AssignmentResponse(
    Guid Id, Guid EligibilityId, Guid StudentId, Guid RoomId, string AcademicYear,
    string Status, Guid AssignedBy, DateTime AssignedAtUtc,
    Guid? CancelledBy, DateTime? CancelledAtUtc, string? CancellationReason);
