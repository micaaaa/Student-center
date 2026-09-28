using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.API.Controllers;

public abstract class WorkflowControllerBase : ControllerBase
{
    protected bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty;

    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
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
