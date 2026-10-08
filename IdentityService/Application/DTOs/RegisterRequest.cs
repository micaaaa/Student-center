using System.ComponentModel.DataAnnotations;

namespace StudentCenter.IdentityService.Application.DTOs;

public sealed class RegisterRequest
{
    [Required, MaxLength(30)]
    public string StudentNumber { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, MinLength(3), MaxLength(50), RegularExpression("^[a-zA-Z0-9._-]+$")]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}
