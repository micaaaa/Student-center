using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Services;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[Route("api/maintenance/workers")]
[Authorize(Policy = "ManageMaintenance")]
public sealed class WorkersController(MaintenanceWorkService service) : MaintenanceControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.GetWorkersAsync(page, pageSize, ct)));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetWorkerAsync(id, ct)));
    }

    [HttpPost]
    public Task<IActionResult> Create(CreateWorkerRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            var result = await service.CreateWorkerAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateWorkerRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.UpdateWorkerAsync(id, request, ct)));
    }
}
