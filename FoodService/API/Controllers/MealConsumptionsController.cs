using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Services;

namespace StudentCenter.FoodService.API.Controllers;

[ApiController]
[Route("api/meal-consumptions")]
[Authorize(Policy = "ManageFood")]
public sealed class MealConsumptionsController(MealUsageService service) : FoodControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetConsumptionAsync(id, ct)));
    }

    [HttpGet]
    public Task<IActionResult> List([FromQuery] Guid studentId, [FromQuery] int year,
        [FromQuery] int month, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () =>
            Ok(await service.GetHistoryAsync(studentId, year, month, page, pageSize, ct)));
    }

    [HttpPost]
    public Task<IActionResult> Consume(ConsumeMealRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
                || actorId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await service.ConsumeAsync(request, actorId, ct);
            if (result.IsReplay)
            {
                return Ok(result.Consumption);
            }

            return CreatedAtAction(nameof(Get), new { id = result.Consumption.Id }, result.Consumption);
        });
    }
}
