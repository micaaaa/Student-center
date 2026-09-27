using System.ComponentModel.DataAnnotations;

namespace StudentCenter.IdentityService.Application.DTOs;

public sealed class UpdatePermissionsRequest
{
    [Required]
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
}
