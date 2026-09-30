using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Services;

namespace StudentCenter.FoodService.API.Controllers;

[ApiController]
[Route("api/restaurants")]
[Authorize]
public sealed class RestaurantsController(FoodCatalogService service) : FoodControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetRestaurantsAsync(false, ct)));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetRestaurantAsync(id, false, ct)));
    }

    [HttpGet("management")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Manage(CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetRestaurantsAsync(true, ct)));
    }

    [HttpGet("management/{id:guid}")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> GetForManagement(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetRestaurantAsync(id, true, ct)));
    }

    [HttpPost]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Create(RestaurantRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            var result = await service.CreateRestaurantAsync(request, ct);
            return CreatedAtAction(nameof(GetForManagement), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ManageFood")]
    public Task<IActionResult> Update(Guid id, RestaurantRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.UpdateRestaurantAsync(id, request, ct)));
    }
}
