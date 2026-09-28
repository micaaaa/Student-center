using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Infrastructure.Persistence;

internal static class StudentMutationWriter
{
    public static async Task SaveAsync(ApplicationDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var applications = db.ChangeTracker.Entries<StudentApplication>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry => entry.Entity).ToArray();
            var documents = db.ChangeTracker.Entries<ApplicationDocument>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Deleted)
                .Select(entry => entry.Entity).ToArray();

            var competitionIds = applications.Select(application => application.CompetitionId).ToHashSet();
            foreach (var applicationId in documents.Select(document => document.ApplicationId).Distinct())
            {
                var application = await db.Applications.AsNoTracking().SingleAsync(item => item.Id == applicationId, ct);
                if (application.Status != ApplicationStatus.Draft)
                    throw new ApplicationConflictException("Documents can only be changed on a draft application.");
                competitionIds.Add(application.CompetitionId);
            }
            foreach (var competitionId in competitionIds)
            {
                var status = await db.Competitions.AsNoTracking().Where(item => item.Id == competitionId)
                    .Select(item => item.Status).SingleAsync(ct);
                if (status == CompetitionStatus.Finalized)
                    throw new ApplicationConflictException("The competition is finalized and cannot be changed.");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApplicationConflictException("The application has changed. Reload it before retrying.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            throw new ApplicationConflictException("The operation conflicted with another change. Reload before retrying.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new ApplicationConflictException("The operation conflicted with another change. Reload before retrying.");
        }
    }
}
