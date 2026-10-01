using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.Application.DTOs;

public sealed record WorkerResponse(Guid Id, Guid UserId, string Name, string Specialization, bool IsActive);

public sealed record MaintenanceActionResponse(
    Guid Id, Guid WorkerId, MaintenanceActionType Type, string Description, DateTime CreatedAtUtc);

// Created from authenticated claims by the API, never bound from a request body.
public sealed record MaintenanceActor(Guid UserId, bool IsSupervisor);
