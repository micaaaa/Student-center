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
public sealed class ApplicationScoresController(ApplicationScoringService service) : ControllerBase
{
    [HttpPut("staff/applications/{applicationId:guid}/score")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> Calculate(
        Guid applicationId, CalculateScoreRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                || userId == Guid.Empty)
                return Unauthorized();

            return Ok(await service.CalculateAsync(applicationId, request, userId, ct));
        });

    [HttpGet("staff/applications/{applicationId:guid}/score")]
    [Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
    public Task<IActionResult> GetForStaff(Guid applicationId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetForStaffAsync(applicationId, ct)));

    [HttpGet("applications/{applicationId:guid}/score")]
    [Authorize(Roles = "STUDENT")]
    public Task<IActionResult> GetMine(Guid applicationId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetMineAsync(applicationId, ct)));

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
