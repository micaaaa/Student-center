using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Infrastructure.Persistence;

namespace StudentCenter.FoodService.API.Controllers;

[ApiController]
[Route("api/meals/overview")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageFood")]
public sealed class OverviewController(FoodDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(1);
        var purchased = await db.MealPurchases.Where(x => x.PurchasedAtUtc >= start && x.PurchasedAtUtc < end)
            .SumAsync(x => (long?)x.Quantity, ct) ?? 0L;
        var consumed = await db.MealConsumptions.LongCountAsync(x => x.ConsumedAtUtc >= start && x.ConsumedAtUtc < end, ct);
        return Ok(new { purchased, consumed });
    }
}
