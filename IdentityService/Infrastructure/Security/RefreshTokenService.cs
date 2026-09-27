using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Domain.Entities;

namespace StudentCenter.IdentityService.Infrastructure.Security;

public sealed class RefreshTokenService(IOptions<JwtSettings> jwtOptions) : IRefreshTokenService
{
    public (string PlainTextToken, RefreshToken RefreshToken) Create(Guid userId)
    {
        var plainTextToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAtUtc = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpirationDays);
        return (plainTextToken, new RefreshToken(userId, Hash(plainTextToken), expiresAtUtc));
    }

    public string Hash(string plainTextToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainTextToken)));
}
