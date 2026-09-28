using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Infrastructure.Repositories;

public sealed class ApplicationReviewRepository(ApplicationDbContext db) : IApplicationReviewRepository
{
    public async Task<IReadOnlyCollection<StudentApplication>> ListAsync(
        Guid? competitionId, ApplicationStatus? status, int page, CancellationToken ct)
    {
        var query = db.Applications.AsNoTracking()
            .Where(application => application.SubmittedAtUtc != null);

        if (competitionId.HasValue)
            query = query.Where(application => application.CompetitionId == competitionId.Value);
        if (status.HasValue)
            query = query.Where(application => application.Status == status.Value);

        return await query.OrderBy(application => application.SubmittedAtUtc)
            .ThenBy(application => application.Id)
            .Skip((page - 1) * 50)
            .Take(50)
            .ToArrayAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApplicationConflictException("The record has changed. Reload it before retrying.");
        }
    }
}
