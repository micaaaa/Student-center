using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Application.Services;

namespace StudentCenter.FoodService.API.Controllers;

[ApiController]
[Route("api/meals/me")]
[Authorize(Roles = "STUDENT")]
public sealed class MyMealsController(MealUsageService service) : FoodControllerBase
{
    [HttpGet("entitlements")]
    public Task<IActionResult> Entitlements([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMineAsync(year, month, ct)));
    }

    [HttpGet("consumptions")]
    public Task<IActionResult> History([FromQuery] int year, [FromQuery] int month, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.GetMyHistoryAsync(year, month, page, pageSize, ct)));
    }
}
