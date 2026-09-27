using StudentCenter.IdentityService.Domain.Enums;

namespace StudentCenter.IdentityService.Domain.Entities;

public sealed class UserPermission
{
    private UserPermission()
    {
    }

    public UserPermission(Guid userId, Permission permission)
    {
        UserId = userId;
        Permission = permission;
    }

    public Guid UserId { get; private set; }
    public Permission Permission { get; private set; }
    public User User { get; private set; } = null!;
}
