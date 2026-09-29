using Microsoft.EntityFrameworkCore;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.Infrastructure.Repositories;

public sealed class AssignmentRepository(AccommodationDbContext db) : IAssignmentRepository
{
    public Task<ReceivedEligibility?> GetEligibilityAsync(Guid id, CancellationToken ct) =>
        db.ReceivedEligibilities.SingleOrDefaultAsync(item => item.Id == id, ct);

    public Task<ReceivedEligibility?> GetByEventAsync(Guid eventId, CancellationToken ct) =>
        db.ReceivedEligibilities.SingleOrDefaultAsync(item => item.EventId == eventId, ct);

    public async Task<IReadOnlyCollection<ReceivedEligibility>> GetEligibilitiesAsync(
        Guid competitionId, CancellationToken ct) =>
        await db.ReceivedEligibilities.AsNoTracking()
            .Where(item => item.CompetitionId == competitionId)
            .OrderBy(item => item.StudentId)
            .ToArrayAsync(ct);

    public async Task AddEligibilityAsync(ReceivedEligibility eligibility, CancellationToken ct)
    {
        await db.ReceivedEligibilities.AddAsync(eligibility, ct);
    }

    public Task<bool> HasActiveAssignmentAsync(Guid studentId, CancellationToken ct) =>
        db.StudentAccommodations.AnyAsync(item => item.StudentId == studentId && item.IsActive, ct);

    public Task<StudentAccommodation?> GetAssignmentAsync(Guid id, CancellationToken ct) =>
        db.StudentAccommodations.Include(item => item.MoveIn).Include(item => item.MoveOut)
            .SingleOrDefaultAsync(item => item.Id == id, ct);

    public async Task<IReadOnlyCollection<StudentAccommodation>> GetHistoryAsync(Guid studentId, CancellationToken ct) =>
        await db.StudentAccommodations.AsNoTracking()
            .Include(item => item.MoveIn).Include(item => item.MoveOut)
            .Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.AssignedAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(ct);

    public async Task AddAssignmentAsync(StudentAccommodation assignment, CancellationToken ct)
    {
        await db.StudentAccommodations.AddAsync(assignment, ct);
    }
}
