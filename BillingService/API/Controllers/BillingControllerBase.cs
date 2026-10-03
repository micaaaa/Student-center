using Microsoft.AspNetCore.Mvc;
using StudentCenter.BillingService.Domain.Exceptions;
using StudentCenter.BillingService.Infrastructure.ExternalServices;

namespace StudentCenter.BillingService.API.Controllers;

public abstract class BillingControllerBase : ControllerBase
{
    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (BillingConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (StudentLookupException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
    }
}
