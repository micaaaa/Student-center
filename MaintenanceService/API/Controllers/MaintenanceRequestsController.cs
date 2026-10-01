using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[Route("api/maintenance/requests")]
[Authorize(Policy = "ManageMaintenance")]
public sealed class MaintenanceRequestsController(MaintenanceRequestService service) : MaintenanceControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] Guid? studentId = null,
        [FromQuery] RequestStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.GetRequestsAsync(studentId, status, page, pageSize, ct)));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetAsync(id, ct)));
    }

    [HttpPost("{id:guid}/accept")]
    public Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
                || actorId == Guid.Empty)
            {
                return Unauthorized();
            }

            return Ok(await service.AcceptAsync(id, actorId, ct));
        });
    }

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, RejectMaintenanceRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
                || actorId == Guid.Empty)
            {
                return Unauthorized();
            }

            return Ok(await service.RejectAsync(id, actorId, request.Reason, ct));
        });
    }

    [HttpPut("{id:guid}/priority")]
    public Task<IActionResult> Priority(Guid id, ChangePriorityRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.ChangePriorityAsync(id, request.Priority, ct)));
    }
}
