using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.BillingService.Domain.Enums;
using StudentCenter.BillingService.Infrastructure.Persistence;

namespace StudentCenter.BillingService.API.Controllers;

[ApiController]
[Route("api/billing/overview")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageBilling")]
public sealed class OverviewController(BillingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var unpaid = db.Charges.Where(x => x.Amount > x.PaidAmount);
        var outstanding = await unpaid.SumAsync(x => (decimal?)(x.Amount - x.PaidAmount), ct) ?? 0m;
        var overdue = await unpaid.Where(x => x.DueDate < today).SumAsync(x => (decimal?)(x.Amount - x.PaidAmount), ct) ?? 0m;
        return Ok(new { outstanding, overdue });
    }
}
