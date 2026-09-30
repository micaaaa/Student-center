using Microsoft.AspNetCore.Mvc;
using StudentCenter.FoodService.Domain.Exceptions;

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
    }
}
