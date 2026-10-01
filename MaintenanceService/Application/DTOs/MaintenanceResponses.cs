using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Entities;

namespace StudentCenter.MaintenanceService.Application.DTOs;

public sealed record CategoryResponse(Guid Id, string Name, string? Description, bool IsActive);

public sealed record MaintenanceRequestResponse(
    Guid Id, Guid StudentId, Guid AccommodationId, Guid RoomId, Guid CategoryId,
    string Title, string Description, RequestPriority Priority, RequestStatus Status,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? ReviewedAtUtc,
    string? RejectionReason, DateTime? CancelledAtUtc,
    Guid? AssignedWorkerId, DateTime? AssignedAtUtc, DateTime? StartedAtUtc,
    DateTime? ResolvedAtUtc, string? ResolutionDescription)
{
    public static MaintenanceRequestResponse From(MaintenanceRequest request)
    {
        return new MaintenanceRequestResponse(request.Id, request.StudentId, request.AccommodationId,
            request.RoomId, request.CategoryId, request.Title, request.Description, request.Priority, request.Status,
            request.CreatedAtUtc, request.UpdatedAtUtc, request.ReviewedAtUtc, request.RejectionReason, request.CancelledAtUtc,
            request.AssignedWorkerId, request.AssignedAtUtc, request.StartedAtUtc,
            request.ResolvedAtUtc, request.ResolutionDescription);
    }
}

public sealed record CurrentAccommodation(Guid Id, Guid RoomId);
