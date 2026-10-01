using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Services;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[Route("api/maintenance/categories")]
[Authorize]
public sealed class CategoriesController(MaintenanceRequestService service) : MaintenanceControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetCategoriesAsync(false, ct)));
    }

    [HttpGet("management")]
    [Authorize(Policy = "ManageMaintenance")]
    public Task<IActionResult> Manage(CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetCategoriesAsync(true, ct)));
    }

    [HttpPost]
    [Authorize(Policy = "ManageMaintenance")]
    public Task<IActionResult> Create(CategoryRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => StatusCode(201, await service.SaveCategoryAsync(null, request, ct)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ManageMaintenance")]
    public Task<IActionResult> Update(Guid id, CategoryRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.SaveCategoryAsync(id, request, ct)));
    }
}
