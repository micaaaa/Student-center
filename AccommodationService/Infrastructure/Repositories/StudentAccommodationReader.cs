using Microsoft.EntityFrameworkCore;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.Infrastructure.Repositories;

public sealed class StudentAccommodationReader(AccommodationDbContext db) : IStudentAccommodationReader
{
    public Task<MyAccommodationResponse?> GetCurrentAsync(Guid studentId, CancellationToken ct) =>
        Query(studentId, currentOnly: true).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyCollection<MyAccommodationResponse>> GetHistoryAsync(Guid studentId, CancellationToken ct) =>
        await Query(studentId, currentOnly: false)
            .ToArrayAsync(ct);

    private IQueryable<MyAccommodationResponse> Query(Guid studentId, bool currentOnly) =>
        from assignment in db.StudentAccommodations.AsNoTracking()
        join room in db.Rooms on assignment.RoomId equals room.Id
        join dorm in db.Dorms on room.DormId equals dorm.Id
        where assignment.StudentId == studentId && (!currentOnly || assignment.IsActive)
        orderby assignment.AssignedAtUtc descending, assignment.Id
        select new MyAccommodationResponse(
            assignment.Id, assignment.AcademicYear, assignment.Status, assignment.AssignedAtUtc,
            assignment.MoveIn == null ? null : assignment.MoveIn.DateUtc,
            assignment.MoveOut == null ? null : assignment.MoveOut.DateUtc,
            assignment.CancelledAtUtc,
            new StudentDormResponse(dorm.Id, dorm.Name, dorm.Address, dorm.City),
            new StudentRoomResponse(room.Id, room.RoomNumber, room.Floor));
}
