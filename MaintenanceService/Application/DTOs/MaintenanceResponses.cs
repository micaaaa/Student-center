using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.Application.DTOs;

public sealed record CategoryResponse(Guid Id, string Name, string? Description, bool IsActive);

public sealed record MaintenanceRequestResponse(
    Guid Id, Guid StudentId, Guid AccommodationId, Guid RoomId, Guid CategoryId,
    string Title, string Description, RequestPriority Priority, RequestStatus Status,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? ReviewedAtUtc,
    string? RejectionReason, DateTime? CancelledAtUtc);

public sealed record CurrentAccommodation(Guid Id, Guid RoomId);
