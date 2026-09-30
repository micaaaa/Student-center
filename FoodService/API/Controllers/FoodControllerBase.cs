using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Domain.Exceptions;
using StudentCenter.FoodService.Application.Services;

namespace StudentCenter.FoodService.API.Controllers;

public abstract class FoodControllerBase : ControllerBase
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
        catch (FoodConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (StudentLookupException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
    }
}
