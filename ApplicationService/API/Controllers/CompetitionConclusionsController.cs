using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Services;

namespace StudentCenter.ApplicationService.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class CompetitionConclusionsController(CompetitionConclusionService service) : WorkflowControllerBase
{
    [HttpPut("staff/competitions/{competitionId:guid}/conclusion-settings")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> Configure(Guid competitionId, ConclusionSettingsRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.ConfigureAsync(competitionId, request, ct)));

    [HttpGet("competitions/{competitionId:guid}/conclusion-settings")]
    public Task<IActionResult> GetSettings(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetSettingsAsync(competitionId, ct)));

    [HttpPut("staff/competitions/{competitionId:guid}/rankings/final")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> Generate(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();
            return Ok(await service.GenerateAsync(competitionId, userId, ct));
        });

    [HttpGet("staff/competitions/{competitionId:guid}/rankings/final")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> GetForStaff(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetForStaffAsync(competitionId, ct)));

    [HttpPost("staff/competitions/{competitionId:guid}/rankings/final/publish")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> Publish(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();
            return Ok(await service.PublishAsync(competitionId, userId, ct));
        });

    [HttpGet("competitions/{competitionId:guid}/rankings/final")]
    [Authorize(Roles = "STUDENT")]
    public Task<IActionResult> GetPublished(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetPublishedAsync(competitionId, ct)));

    [HttpGet("competitions/{competitionId:guid}/eligibility/me")]
    [Authorize(Roles = "STUDENT")]
    public Task<IActionResult> GetMyEligibility(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetMyEligibilityAsync(competitionId, ct)));

    [HttpGet("staff/competitions/{competitionId:guid}/eligibility")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> GetDecisions(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetDecisionsAsync(competitionId, ct)));
}
