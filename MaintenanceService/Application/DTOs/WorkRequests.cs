using System.ComponentModel.DataAnnotations;

namespace StudentCenter.MaintenanceService.Application.DTOs;

public sealed class CreateWorkerRequest
{
    public Guid UserId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Specialization { get; set; } = string.Empty;
}

public sealed class UpdateWorkerRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Specialization { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public sealed class AssignWorkerRequest
{
    public Guid WorkerId { get; set; }
}

public sealed class InterventionRequest
{
    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;
}
