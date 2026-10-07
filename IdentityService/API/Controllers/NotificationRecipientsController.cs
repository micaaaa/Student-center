using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.IdentityService.Domain.Enums;
using StudentCenter.IdentityService.Infrastructure.Persistence;
using StudentCenter.Security;

namespace StudentCenter.IdentityService.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/notification-recipients")]
[NotificationServiceKey]
public sealed class NotificationRecipientsController(IdentityDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var recipient = await db.Users.AsNoTracking()
            .Where(user => user.Id == id && user.Status == AccountStatus.Active)
            .Select(user => new { user.Email }).SingleOrDefaultAsync(ct);
        return recipient is null ? NotFound() : Ok(recipient);
    }
}
