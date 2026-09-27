using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Application.Exceptions;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Domain.Entities;
using StudentCenter.IdentityService.Domain.Enums;
using Microsoft.Extensions.Options;
using StudentCenter.IdentityService.Infrastructure.Security;

namespace StudentCenter.IdentityService.Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenService refreshTokenService,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await userRepository.GetByUsernameAsync(username, cancellationToken) is not null)
        {
            throw new ConflictException("Username is already in use.");
        }

        if (await userRepository.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Email is already in use.");
        }

        var user = new User(username, email, passwordHasher.Hash(request.Password), UserRole.Student);
        await userRepository.AddAsync(user, cancellationToken);
        return await CreateResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || user.Status != AccountStatus.Active || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        user.RecordSuccessfulLogin();
        await userRepository.SaveChangesAsync(cancellationToken);
        return await CreateResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokenRepository.GetByTokenHashAsync(refreshTokenService.Hash(request.RefreshToken), cancellationToken);
        if (refreshToken is null || !refreshToken.IsActive || refreshToken.User.Status != AccountStatus.Active)
        {
            throw new InvalidCredentialsException();
        }

        refreshToken.Revoke();
        await userRepository.SaveChangesAsync(cancellationToken);
        return await CreateResponseAsync(refreshToken.User, cancellationToken);
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokenRepository.GetByTokenHashAsync(refreshTokenService.Hash(request.RefreshToken), cancellationToken);
        if (refreshToken is not null && refreshToken.IsActive)
        {
            refreshToken.Revoke();
            await userRepository.SaveChangesAsync(cancellationToken);
        }
    }

    public UserResponse ToUserResponse(User user) => new(
        user.Id,
        user.Username,
        user.Email,
        user.Role.ToString().ToUpperInvariant(),
        user.Status.ToString().ToUpperInvariant(),
        user.Permissions.Select(permission => permission.Permission.ToString()).ToArray());

    private async Task<AuthResponse> CreateResponseAsync(User user, CancellationToken cancellationToken)
    {
        var created = refreshTokenService.Create(user.Id);
        await refreshTokenRepository.AddAsync(created.RefreshToken, cancellationToken);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpirationMinutes);
        return new AuthResponse(jwtTokenGenerator.CreateAccessToken(user), created.PlainTextToken, expiresAtUtc, ToUserResponse(user));
    }
}
