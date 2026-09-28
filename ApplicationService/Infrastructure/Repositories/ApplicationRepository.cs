using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Infrastructure.Repositories;

public sealed class ApplicationRepository(ApplicationDbContext db) : IApplicationRepository
{
    public Task<StudentApplication?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Applications.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<StudentApplication?> GetForStudentAsync(Guid c, Guid s, CancellationToken ct = default) =>
        db.Applications.SingleOrDefaultAsync(x => x.CompetitionId == c && x.StudentId == s, ct);

    public async Task AddAsync(StudentApplication a, CancellationToken ct = default)
    {
        await db.Applications.AddAsync(a, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<StudentApplication>> GetMineAsync(Guid studentId, CancellationToken ct = default) =>
        await db.Applications.AsNoTracking().Where(x => x.StudentId == studentId).OrderByDescending(x => x.CreatedAtUtc)
        .ThenBy(x => x.Id)
        .ToArrayAsync(ct);

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApplicationConflictException("The application has changed. Reload it before retrying.");
        }
    }
}
