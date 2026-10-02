using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.NotificationService.Infrastructure.ExternalServices;
using NotificationApplicationService = StudentCenter.NotificationService.Application.Services.NotificationService;

namespace StudentCenter.NotificationService.API.Controllers;

[ApiController]
[Route("api/notifications/me")]
[Authorize]
public sealed class NotificationsController(NotificationApplicationService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.ListAsync(unreadOnly, page, pageSize, ct)));
    }

    [HttpGet("unread-count")]
    public Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(new { count = await service.CountUnreadAsync(ct) }));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetAsync(id, ct)));
    }

    [HttpPut("{id:guid}/read")]
    public Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            await service.MarkReadAsync(id, ct);
            return NoContent();
        });
    }

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
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (OwnerLookupException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
    }
}
