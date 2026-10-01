using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[Route("api/maintenance/requests/me")]
[Authorize(Roles = "STUDENT")]
public sealed class MyMaintenanceController(MaintenanceRequestService service) : MaintenanceControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] RequestStatus? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.GetMineAsync(status, page, pageSize, ct)));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMineAsync(id, ct)));
    }

    [HttpPost]
    public Task<IActionResult> Submit(SubmitMaintenanceRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            var result = await service.SubmitAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });
    }

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.CancelMineAsync(id, ct)));
    }
}
