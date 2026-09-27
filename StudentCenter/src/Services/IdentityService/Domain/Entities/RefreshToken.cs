namespace StudentCenter.IdentityService.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public User User { get; private set; } = null!;

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public void Revoke() => RevokedAtUtc ??= DateTime.UtcNow;
}
