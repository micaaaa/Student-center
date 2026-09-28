using StudentCenter.IdentityService.Application.DTOs;

namespace StudentCenter.IdentityService.Application.Interfaces;

public interface IUserManagementService
{
    Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserResponse> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task<UserResponse> UpdatePermissionsAsync(Guid id, UpdatePermissionsRequest request, CancellationToken cancellationToken = default);

    Task<UserResponse> SetAccountStatusAsync(Guid id, bool active, CancellationToken cancellationToken = default);
}
