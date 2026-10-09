using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.Security;
using StudentCenter.StudentService.Infrastructure.Persistence;

namespace StudentCenter.StudentService.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/notification-recipients")]
[NotificationServiceKey]
public sealed class NotificationRecipientsController(StudentDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var recipient = await db.Students.AsNoTracking().Where(student => student.Id == id && !student.IsDeleted)
            .Select(student => new { student.Email }).SingleOrDefaultAsync(ct);
        return recipient is null ? NotFound() : Ok(recipient);
    }
}
