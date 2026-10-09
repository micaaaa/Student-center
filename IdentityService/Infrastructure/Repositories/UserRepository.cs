using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Domain.Entities;
using StudentCenter.IdentityService.Infrastructure.Persistence;

namespace StudentCenter.IdentityService.Infrastructure.Repositories;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public async Task<IReadOnlyCollection<StaffDirectoryEntry>> SearchStaffAsync(
        string? search, int page, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking().Where(user =>
            (user.Role == UserRole.Staff || user.Role == UserRole.Admin) && user.Status == AccountStatus.Active);
        foreach (var term in (search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            query = query.Where(user => user.Username.Contains(term) || user.Email.Contains(term));

        return await query.OrderBy(user => user.Username).ThenBy(user => user.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(user => new StaffDirectoryEntry(user.Id, user.Username, user.Email))
            .ToArrayAsync(cancellationToken);
    }
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.Include(user => user.Permissions).SingleOrDefaultAsync(user => user.Id == id && !user.IsDeleted, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.Include(user => user.Permissions).SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.Include(user => user.Permissions).SingleOrDefaultAsync(user => user.Username == username, cancellationToken);

    public async Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Users.Include(user => user.Permissions).Where(user => !user.IsDeleted).OrderBy(user => user.Username).ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException
            { Number: 2601 or 2627 })
        {
            throw new StudentCenter.IdentityService.Application.Exceptions.ConflictException(
                "An account with this username or email already exists. Retry registration or sign in.");
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var administrators = await dbContext.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Admin && user.Status == AccountStatus.Active
                && user.Permissions.Any(permission => permission.Permission == Permission.ManageUsers))
            .Select(user => user.Id).ToListAsync(cancellationToken);
        if (administrators.Count > 0)
        {
            var remaining = administrators.Any(id =>
            {
                var tracked = dbContext.Users.Local.FirstOrDefault(user => user.Id == id);
                return tracked is null || (tracked.Role == UserRole.Admin && tracked.Status == AccountStatus.Active
                    && tracked.Permissions.Any(permission => permission.Permission == Permission.ManageUsers));
            });
            if (!remaining)
                throw new StudentCenter.IdentityService.Application.Exceptions.ConflictException(
                    "At least one active administrator with user management permission must remain.");
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
