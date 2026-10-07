using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.API.Controllers;

[ApiController]
[Route("api/staff/applications/overview")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
public sealed class OverviewController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var submitted = await db.Applications.CountAsync(x => x.Status == ApplicationStatus.Submitted, ct);
        var review = await db.Applications.CountAsync(x => x.Status == ApplicationStatus.UnderReview, ct);
        return Ok(new { submitted, underReview = review });
    }
}
