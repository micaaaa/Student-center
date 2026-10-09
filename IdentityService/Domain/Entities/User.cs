using StudentCenter.IdentityService.Domain.Enums;

namespace StudentCenter.IdentityService.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    public User(string username, string email, string passwordHash, UserRole role, bool registrationPending = false)
    {
        Id = Guid.NewGuid();
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        RegistrationPending = registrationPending;
        Status = AccountStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public bool RegistrationPending { get; private set; }
    public bool DeletionPending { get; private set; }
    public bool IsDeleted { get; private set; }
    public ICollection<UserPermission> Permissions { get; private set; } = new List<UserPermission>();

    public void RecordSuccessfulLogin() => LastLoginAtUtc = DateTime.UtcNow;

    public void CompleteRegistration() => RegistrationPending = false;

    public void ChangeRole(UserRole role) => Role = role;

    public void Deactivate() => Status = AccountStatus.Inactive;

    public void Activate()
    {
        if (DeletionPending || IsDeleted)
            throw new Application.Exceptions.ConflictException("Account deletion must be completed before any further changes.");
        Status = AccountStatus.Active;
    }

    public void BeginDeletion()
    {
        DeletionPending = true;
        Deactivate();
    }

    public void Anonymize()
    {
        Username = "deleted-" + Id.ToString("N");
        Email = Username + "@deleted.invalid";
        PasswordHash = string.Empty;
        Permissions.Clear();
        RegistrationPending = false;
        DeletionPending = false;
        IsDeleted = true;
        Deactivate();
    }

    public void ReplacePermissions(IEnumerable<Permission> selectedPermissions)
    {
        Permissions.Clear();
        foreach (var permission in selectedPermissions.Distinct())
        {
            Permissions.Add(new UserPermission(Id, permission));
        }
    }
}
