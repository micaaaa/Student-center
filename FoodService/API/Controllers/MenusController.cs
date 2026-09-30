using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Services;

namespace StudentCenter.FoodService.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class MenusController(FoodCatalogService service) : FoodControllerBase
{
    [HttpGet("restaurants/{restaurantId:guid}/menus")]
    public Task<IActionResult> List(Guid restaurantId, [FromQuery] DateOnly from,
        [FromQuery] DateOnly to, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
            Ok(await service.GetMenusAsync(restaurantId, from, to, false, ct)));
    }

    [HttpGet("menus/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMenuAsync(id, false, ct)));
    }

    [HttpGet("restaurants/{restaurantId:guid}/menus/management")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Manage(Guid restaurantId, [FromQuery] DateOnly from,
        [FromQuery] DateOnly to, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
            Ok(await service.GetMenusAsync(restaurantId, from, to, true, ct)));
    }

    [HttpGet("menus/management/{id:guid}")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> GetForManagement(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMenuAsync(id, true, ct)));
    }

    [HttpPost("restaurants/{restaurantId:guid}/menus")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Create(Guid restaurantId, MenuRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            var result = await service.CreateMenuAsync(restaurantId, request, ct);
            return CreatedAtAction(nameof(GetForManagement), new { id = result.Id }, result);
        });
    }

    [HttpPut("menus/{id:guid}")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Update(Guid id, MenuRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.UpdateMenuAsync(id, request, ct)));
    }

    [HttpPost("menus/{id:guid}/publish")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.PublishMenuAsync(id, ct)));
    }

    [HttpPost("menus/{id:guid}/withdraw")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.WithdrawMenuAsync(id, ct)));
    }
}
