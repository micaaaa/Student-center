using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.AccommodationService.Application.Services;

namespace StudentCenter.AccommodationService.API.Controllers;

[ApiController]
[Route("api/accommodations/me")]
[Authorize(Roles = "STUDENT")]
public sealed class MyAccommodationController(MyAccommodationService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Current(CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetCurrentAsync(ct)));

    [HttpGet("history")]
    public Task<IActionResult> History(CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetHistoryAsync(ct)));

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
        catch (StudentLookupException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
    }
}
