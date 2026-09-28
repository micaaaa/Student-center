using System.Data;
using Microsoft.Data.SqlClient;
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
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var applicationIds = db.ChangeTracker.Entries<ApplicationDocument>()
                .Where(entry => entry.State == EntityState.Modified)
                .Select(entry => entry.Entity.ApplicationId).Distinct().ToArray();
            foreach (var applicationId in applicationIds)
            {
                var status = await db.Applications.AsNoTracking()
                    .Where(application => application.Id == applicationId)
                    .Select(application => application.Status).SingleAsync(ct);
                if (status != ApplicationStatus.UnderReview)
                    throw new ApplicationConflictException("The application is no longer under review.");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApplicationConflictException("The record has changed. Reload it before retrying.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            throw new ApplicationConflictException("The review conflicted with another operation. Reload before retrying.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new ApplicationConflictException("The review conflicted with another operation. Reload before retrying.");
        }
    }
}
