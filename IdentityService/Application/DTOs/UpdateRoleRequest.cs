using System.ComponentModel.DataAnnotations;

namespace StudentCenter.IdentityService.Application.DTOs;

public sealed class UpdateRoleRequest
{
    [Required]
    public string Role { get; init; } = string.Empty;
}
