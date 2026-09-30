using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Application.Services;

namespace StudentCenter.FoodService.API.Controllers;

[ApiController]
[Route("api/meals/me/purchases")]
[Authorize(Roles = "STUDENT")]
public sealed class MyMealPurchasesController(MealPurchaseService service) : FoodControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] int year, [FromQuery] int month, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () =>
            Ok(await service.GetMyHistoryAsync(year, month, page, pageSize, ct)));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMineAsync(id, ct)));
    }
}
