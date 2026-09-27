namespace StudentCenter.IdentityService.Infrastructure.Persistence;

public sealed class InitialAdminSettings
{
    public const string SectionName = "InitialAdmin";

    public string Username { get; set; } = "admin";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
