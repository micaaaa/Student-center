using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class PreliminaryRankingsController(PreliminaryRankingService service) : ControllerBase
{
    [HttpPut("staff/competitions/{competitionId:guid}/rankings/preliminary")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> Generate(
        Guid competitionId, GenerateRankingRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            return Ok(await service.GenerateAsync(competitionId, request.TieRule, userId, ct));
        });

    [HttpGet("staff/competitions/{competitionId:guid}/rankings/preliminary")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> GetForStaff(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetForStaffAsync(competitionId, ct)));

    [HttpPost("staff/competitions/{competitionId:guid}/rankings/preliminary/publish")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> Publish(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            return Ok(await service.PublishAsync(competitionId, userId, ct));
        });

    [HttpGet("competitions/{competitionId:guid}/rankings/preliminary")]
    [Authorize(Roles = "STUDENT")]
    public Task<IActionResult> GetPublished(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetPublishedAsync(competitionId, ct)));

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty;

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ApplicationConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { message = "Student service is currently unavailable." });
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(503, new { message = "Student service request timed out." });
        }
    }
}
