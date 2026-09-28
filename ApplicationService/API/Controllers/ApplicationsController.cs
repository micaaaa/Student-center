using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentApplicationService = StudentCenter.ApplicationService.Application.Services.ApplicationService;

namespace StudentCenter.ApplicationService.API.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize(Roles = "STUDENT")]
public sealed class ApplicationsController(StudentApplicationService service) : ControllerBase
{
    [HttpGet("me")]
    public Task<ActionResult<IReadOnlyCollection<ApplicationResponse>>> GetMine(CancellationToken ct) =>
        ExecuteAsync(() => service.GetMineAsync(ct));

    [HttpGet("{id:guid}")]
    public Task<ActionResult<ApplicationResponse>> Get(Guid id, CancellationToken ct) =>
        ExecuteAsync(() => service.GetAsync(id, ct));

    [HttpPost]
    public Task<ActionResult<ApplicationResponse>> Create(CreateApplicationRequest request, CancellationToken ct) =>
        ExecuteAsync(() => service.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public Task<ActionResult<ApplicationResponse>> Update(Guid id, UpdateApplicationRequest request, CancellationToken ct) =>
        ExecuteAsync(() => service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/submit")]
    public Task<ActionResult<ApplicationResponse>> Submit(Guid id, CancellationToken ct) =>
        ExecuteAsync(() => service.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/withdraw")]
    public Task<ActionResult<ApplicationResponse>> Withdraw(Guid id, CancellationToken ct) =>
        ExecuteAsync(() => service.WithdrawAsync(id, ct));

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ApplicationConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Student service is currently unavailable." });
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Student service request timed out." });
        }
    }
}
