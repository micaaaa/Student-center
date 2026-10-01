using System.ComponentModel.DataAnnotations;
using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.Application.DTOs;

public sealed class CategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SubmitMaintenanceRequest
{
    public Guid CategoryId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(RequestPriority))]
    public RequestPriority Priority { get; set; } = RequestPriority.Medium;
}

public sealed class RejectMaintenanceRequest
{
    [Required, MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ChangePriorityRequest
{
    [EnumDataType(typeof(RequestPriority))]
    public RequestPriority Priority { get; set; }
}
