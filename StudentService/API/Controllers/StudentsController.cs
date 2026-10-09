using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentCenter.StudentService.Application.DTOs;
using StudentCenter.StudentService.Application.Exceptions;
using StudentCenter.StudentService.Application.Interfaces;

namespace StudentCenter.StudentService.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public sealed class StudentsController(IStudentService studentService,
    Infrastructure.Persistence.StudentDbContext db,
    Infrastructure.ExternalServices.StudentAccountDirectory accounts) : ControllerBase
{
    [HttpGet("directory")]
    [Authorize(Roles = "STAFF,ADMIN")]
    public async Task<IActionResult> Directory(CancellationToken ct)
    {
        try
        {
            var ids = await accounts.GetStudentAccountsAsync(ct);
            return Ok(await db.Students.AsNoTracking()
                .Where(student => !student.IsDeleted && ids.Contains(student.UserId))
                .Select(student => new { student.Id, student.UserId, student.FirstName, student.LastName, student.StudentNumber })
                .ToArrayAsync(ct));
        }
        catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException && !ct.IsCancellationRequested)
        {
            return StatusCode(503, new { message = "Account directory is temporarily unavailable." });
        }
    }
    [HttpGet]
    [Authorize(Roles = "STAFF,ADMIN")]
    public async Task<ActionResult<StudentSearchResponse>> Search(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100 || search?.Length > 150)
            return BadRequest(new { message = "Invalid student search parameters." });

        try
        {
            return Ok(await studentService.SearchAsync(search, page, pageSize, cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException
            || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StatusCode(503, new { message = "Account directory is temporarily unavailable." });
        }
    }
    [HttpPost("me")]
    [Authorize(Roles = "STUDENT")]
    public async Task<ActionResult<StudentResponse>> CreateMyProfile(CreateStudentProfileRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(
        () => studentService.CreateAsync(GetCurrentUserId(), request, cancellationToken),
        StatusCodes.Status201Created);

    [HttpGet("me")]
    [Authorize(Roles = "STUDENT")]
    public async Task<ActionResult<StudentResponse>> GetMyProfile(CancellationToken cancellationToken) =>
        await ExecuteAsync(() => studentService.GetMyProfileAsync(GetCurrentUserId(), cancellationToken));

    [HttpPut("me")]
    [Authorize(Roles = "STUDENT")]
    public async Task<ActionResult<StudentResponse>> UpdateMyProfile(UpdateStudentProfileRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(() => studentService.UpdateMyProfileAsync(GetCurrentUserId(), request, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "STAFF,ADMIN")]
    public async Task<ActionResult<StudentResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(() => studentService.GetByIdAsync(id, cancellationToken));

    [HttpGet("{id:guid}/summary")]
    [Authorize(Roles = "STAFF,ADMIN")]
    public async Task<ActionResult<StudentSummaryResponse>> GetSummary(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await studentService.GetSummaryAsync(id, cancellationToken));
        }
        catch (NotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<ActionResult<StudentResponse>> ExecuteAsync(Func<Task<StudentResponse>> action, int successStatus = StatusCodes.Status200OK)
    {
        try
        {
            return StatusCode(successStatus, await action());
        }
        catch (NotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
