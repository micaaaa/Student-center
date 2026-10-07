using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.API.Controllers;

[ApiController]
[Route("api/accommodations/overview")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageAccommodation")]
public sealed class OverviewController(AccommodationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var reserved = await db.StudentAccommodations.CountAsync(x => x.Status == "ASSIGNED", ct);
        var residents = await db.StudentAccommodations.CountAsync(x => x.Status == "ACTIVE", ct);
        return Ok(new { reserved, residents });
    }
}
