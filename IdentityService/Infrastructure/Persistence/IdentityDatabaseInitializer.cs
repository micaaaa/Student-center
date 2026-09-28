using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Domain.Entities;
using StudentCenter.IdentityService.Domain.Enums;

namespace StudentCenter.IdentityService.Infrastructure.Persistence;

public sealed class IdentityDatabaseInitializer(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<InitialAdminSettings> initialAdminOptions)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        var settings = initialAdminOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
            return;
        if (await dbContext.Users.AnyAsync(user => user.Email == settings.Email.ToLowerInvariant(), cancellationToken))
            return;
        var admin = new User(
            settings.Username.ToLowerInvariant(),
            settings.Email.ToLowerInvariant(),
            passwordHasher.Hash(settings.Password),
            UserRole.Admin);
        admin.ReplacePermissions(Enum.GetValues<Permission>());
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
