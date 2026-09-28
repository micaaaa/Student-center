namespace StudentCenter.IdentityService.Application.DTOs;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string Role,
    string Status,
    IReadOnlyCollection<string> Permissions);
