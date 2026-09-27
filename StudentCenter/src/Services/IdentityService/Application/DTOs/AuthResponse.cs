namespace StudentCenter.IdentityService.Application.DTOs;

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserResponse User);
