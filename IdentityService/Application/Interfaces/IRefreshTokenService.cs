using StudentCenter.IdentityService.Domain.Entities;

namespace StudentCenter.IdentityService.Application.Interfaces;

public interface IRefreshTokenService
{
    (string PlainTextToken, RefreshToken RefreshToken) Create(Guid userId);

    string Hash(string plainTextToken);
}
