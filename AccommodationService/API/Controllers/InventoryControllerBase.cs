using Microsoft.AspNetCore.Mvc;
using StudentCenter.AccommodationService.Domain.Exceptions;

namespace StudentCenter.AccommodationService.API.Controllers;

public abstract class InventoryControllerBase : ControllerBase
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
        catch (AccommodationConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
