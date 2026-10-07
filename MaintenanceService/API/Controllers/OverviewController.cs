using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[Route("api/maintenance/overview")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageMaintenance")]
public sealed class OverviewController(MaintenanceDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var submitted = await db.Requests.CountAsync(x => x.Status == RequestStatus.Submitted, ct);
        var ongoing = await db.Requests.CountAsync(x => x.Status == RequestStatus.Accepted
            || x.Status == RequestStatus.Assigned || x.Status == RequestStatus.InProgress, ct);
        return Ok(new { submitted, ongoing });
    }
}
