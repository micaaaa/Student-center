using System.ComponentModel.DataAnnotations;

namespace StudentCenter.IdentityService.Application.DTOs;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
