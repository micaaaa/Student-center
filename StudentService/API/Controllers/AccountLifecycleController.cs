using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.StudentService.Infrastructure.Persistence;

namespace StudentCenter.StudentService.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/accounts")]
[Authorize]
public sealed class AccountLifecycleController(StudentDbContext db) : ControllerBase
{
    [HttpPost("{id:guid}/anonymize")]
    public async Task<IActionResult> Anonymize(Guid id, CancellationToken ct)
    {
        if (User.FindFirst("account_anonymization")?.Value != id.ToString())
            return Forbid();
        var student = await db.Students.SingleOrDefaultAsync(item => item.UserId == id, ct);
        if (student is not null)
        {
            student.Anonymize();
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}
