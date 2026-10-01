using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.MaintenanceService.Application.Services;

namespace StudentCenter.MaintenanceService.API.Controllers;

[ApiController]
[Route("api/maintenance/requests/me/{id:guid}/actions")]
[Authorize(Roles = "STUDENT")]
public sealed class MyMaintenanceActionsController(MaintenanceWorkService service) : MaintenanceControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(Guid id, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.GetMyStudentActionsAsync(id, page, pageSize, ct)));
    }
}
