using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.API.Controllers;

[ApiController]
[Route("api/accommodations/billing-options")]
[Authorize(Policy = "ManageBilling")]
public sealed class BillingAccommodationsController(AccommodationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid studentId, CancellationToken ct)
    {
        if (studentId == Guid.Empty)
            return BadRequest("Select a student.");

        var options = await (
            from accommodation in db.StudentAccommodations.AsNoTracking()
            join room in db.Rooms on accommodation.RoomId equals room.Id
            join dorm in db.Dorms on room.DormId equals dorm.Id
            where accommodation.StudentId == studentId && accommodation.Status != "CANCELLED"
            orderby accommodation.IsActive descending, accommodation.AssignedAtUtc descending
            select new
            {
                accommodation.Id,
                accommodation.AcademicYear,
                accommodation.Status,
                accommodation.IsActive,
                room.RoomNumber,
                DormName = dorm.Name
            }).ToListAsync(ct);

        return Ok(options);
    }
}
