using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[Route("api/maintenance/work")]
[Authorize(Roles = "STAFF,ADMIN")]
public sealed class MaintenanceWorkController(MaintenanceWorkService service) : MaintenanceControllerBase
{
    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMyWorkerAsync(Actor(), ct)));
    }

    [HttpGet("requests")]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] RequestStatus? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.GetMyTasksAsync(Actor(), status, page, pageSize, ct)));
    }

    [HttpGet("requests/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetTaskAsync(id, Actor(), ct)));
    }

    [HttpPost("/api/maintenance/requests/{id:guid}/assign")]
    [Authorize(Policy = "ManageMaintenance")]
    public Task<IActionResult> Assign(Guid id, AssignWorkerRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.AssignAsync(id, request.WorkerId, Actor(), ct)));
    }

    [HttpPost("requests/{id:guid}/start")]
    public Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.StartAsync(id, Actor(), ct)));
    }

    [HttpPost("requests/{id:guid}/actions")]
    public Task<IActionResult> Intervene(Guid id, InterventionRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.InterveneAsync(id, request.Description, Actor(), ct)));
    }

    [HttpPost("requests/{id:guid}/resolve")]
    public Task<IActionResult> Resolve(Guid id, InterventionRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.ResolveAsync(id, request.Description, Actor(), ct)));
    }

    [HttpGet("requests/{id:guid}/actions")]
    public Task<IActionResult> Actions(Guid id, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.GetActionsAsync(id, Actor(), page, pageSize, ct)));
    }

    private MaintenanceActor Actor()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
        {
            throw new MaintenanceAccessException("A valid authenticated staff identity is required.");
        }

        return new MaintenanceActor(id, User.HasClaim("permission", "ManageMaintenance"));
    }
}
