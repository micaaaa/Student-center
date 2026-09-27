using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.StudentService.Application.DTOs;
using StudentCenter.StudentService.Application.Exceptions;
using StudentCenter.StudentService.Application.Interfaces;

namespace StudentCenter.StudentService.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public sealed class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpPost("me")]
    [Authorize(Roles = "STUDENT")]
    public async Task<ActionResult<StudentResponse>> CreateMyProfile(CreateStudentProfileRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(() => studentService.CreateAsync(GetCurrentUserId(), request, cancellationToken), StatusCodes.Status201Created);

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
        try { return Ok(await studentService.GetSummaryAsync(id, cancellationToken)); }
        catch (NotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<ActionResult<StudentResponse>> ExecuteAsync(Func<Task<StudentResponse>> action, int successStatus = StatusCodes.Status200OK)
    {
        try { return StatusCode(successStatus, await action()); }
        catch (NotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ConflictException exception) { return Conflict(new { message = exception.Message }); }
    }
}
