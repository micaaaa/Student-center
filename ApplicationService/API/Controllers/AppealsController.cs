using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Services;

namespace StudentCenter.ApplicationService.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class AppealsController(AppealService service) : WorkflowControllerBase
{
    [HttpPost("applications/{applicationId:guid}/appeal")]
    [Authorize(Roles = "STUDENT")]
    public Task<IActionResult> Submit(Guid applicationId, SubmitAppealRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var appeal = await service.SubmitAsync(applicationId, request.Reason, ct);
            return CreatedAtAction(nameof(GetMine), new { applicationId }, appeal);
        });

    [HttpGet("applications/{applicationId:guid}/appeal")]
    [Authorize(Roles = "STUDENT")]
    public Task<IActionResult> GetMine(Guid applicationId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetMineAsync(applicationId, ct)));

    [HttpGet("staff/competitions/{competitionId:guid}/appeals")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> List(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(competitionId, ct)));

    [HttpPost("staff/appeals/{appealId:guid}/start-review")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> StartReview(Guid appealId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.StartReviewAsync(appealId, ct)));

    [HttpPut("staff/appeals/{appealId:guid}/resolution")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> Resolve(Guid appealId, ResolveAppealRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();
            return Ok(await service.ResolveAsync(appealId, request, userId, ct));
        });
}
