using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/accounts")]
[Authorize]
public sealed class AccountLifecycleController(MaintenanceDbContext db) : ControllerBase
{
    [HttpPost("{id:guid}/anonymize")]
    public async Task<IActionResult> Anonymize(Guid id, CancellationToken ct)
    {
        if (User.FindFirst("account_anonymization")?.Value != id.ToString())
            return Forbid();
        var worker = await db.Workers.SingleOrDefaultAsync(item => item.UserId == id, ct);
        if (worker is not null)
        {
            worker.Update("Deleted user", "Not available", false);
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}
