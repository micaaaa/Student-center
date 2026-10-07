using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Application.Exceptions;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Domain.Enums;

namespace StudentCenter.IdentityService.Application.Services;

public sealed class UserManagementService(IUserRepository userRepository, IAuthService authService) : IUserManagementService
{
    public Task<IReadOnlyCollection<StaffDirectoryEntry>> SearchStaffAsync(
        string? search, int page, CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > 100000 || search?.Length > 150)
            throw new ArgumentException("Invalid staff search parameters.");
        return userRepository.SearchStaffAsync(search?.Trim(), page, cancellationToken);
    }
    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await userRepository.GetAllAsync(cancellationToken)).Select(authService.ToUserResponse).ToArray();

    public async Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        authService.ToUserResponse(await GetUserAsync(id, cancellationToken));

    public async Task<UserResponse> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || !Enum.IsDefined(role))
            throw new ConflictException("Role is not valid.");
        var user = await GetUserAsync(id, cancellationToken);
        user.ChangeRole(role);
        if (role == UserRole.Student) user.ReplacePermissions([]);
        await userRepository.SaveChangesAsync(cancellationToken);
        return authService.ToUserResponse(user);
    }

    public async Task<UserResponse> UpdatePermissionsAsync(Guid id, UpdatePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Permissions.All(value => Enum.TryParse<Permission>(value, true, out var permission) && Enum.IsDefined(permission)))
        {
            throw new ConflictException("One or more permissions are not valid.");
        }

        var user = await GetUserAsync(id, cancellationToken);
        if (user.Role == UserRole.Student && request.Permissions.Count > 0)
            throw new ConflictException("Student accounts cannot have staff permissions.");
        var permissions = request.Permissions.Select(value => Enum.Parse<Permission>(value, true));
        user.ReplacePermissions(permissions);
        await userRepository.SaveChangesAsync(cancellationToken);
        return authService.ToUserResponse(user);
    }

    public async Task<UserResponse> SetAccountStatusAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(id, cancellationToken);
        if (active)
            user.Activate();
        else
            user.Deactivate();
        await userRepository.SaveChangesAsync(cancellationToken);
        return authService.ToUserResponse(user);
    }

    private async Task<Domain.Entities.User> GetUserAsync(Guid id, CancellationToken cancellationToken) =>
        await userRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User was not found.");
}
