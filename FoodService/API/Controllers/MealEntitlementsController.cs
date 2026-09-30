using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Services;

namespace StudentCenter.FoodService.API.Controllers;

[ApiController]
[Route("api/meal-entitlements")]
[Authorize(Policy = "ManageFood")]
public sealed class MealEntitlementsController(MealUsageService service) : FoodControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetAsync(id, ct)));
    }

    [HttpGet]
    public Task<IActionResult> List([FromQuery] Guid studentId, [FromQuery] int year,
        [FromQuery] int month, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetForStudentAsync(studentId, year, month, ct)));
    }

    [HttpPost]
    public Task<IActionResult> Create(CreateMealEntitlementRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
                || actorId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await service.CreateAsync(request, actorId, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateMealEntitlementRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
                || actorId == Guid.Empty)
            {
                return Unauthorized();
            }

            return Ok(await service.UpdateAsync(id, request, actorId, ct));
        });
    }
}
