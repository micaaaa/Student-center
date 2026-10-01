using Microsoft.AspNetCore.Mvc;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.API.Controllers;

public abstract class MaintenanceControllerBase : ControllerBase
{
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
        catch (MaintenanceConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ServiceLookupException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
        catch (MaintenanceAccessException exception)
        {
            return StatusCode(403, new { message = exception.Message });
        }
    }
}
