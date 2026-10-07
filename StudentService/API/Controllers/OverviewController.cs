using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.StudentService.Domain.Enums;
using StudentCenter.StudentService.Infrastructure.Persistence;

namespace StudentCenter.StudentService.API.Controllers;

[ApiController]
[Route("api/students/overview")]
[Authorize(Roles = "STAFF,ADMIN")]
public sealed class OverviewController(StudentDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var total = await db.Students.CountAsync(ct);
        var active = await db.Students.CountAsync(x => x.Status == StudentStatus.Active, ct);
        return Ok(new { total, active });
    }
}
