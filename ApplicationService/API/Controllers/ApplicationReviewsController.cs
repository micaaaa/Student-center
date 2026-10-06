using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.API.Controllers;

[ApiController]
[Route("api/staff/applications")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageApplications")]
public sealed class ApplicationReviewsController(ApplicationReviewService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(
        CancellationToken ct, Guid? competitionId = null, ApplicationStatus? status = null, int page = 1, Guid? studentId = null) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(competitionId, status, page, ct, studentId)));

    [HttpGet("{applicationId:guid}")]
    public Task<IActionResult> Get(Guid applicationId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(applicationId, ct)));

    [HttpPost("{applicationId:guid}/start-review")]
    public Task<IActionResult> StartReview(Guid applicationId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.StartReviewAsync(applicationId, ct)));

    [HttpGet("{applicationId:guid}/documents")]
    public Task<IActionResult> ListDocuments(Guid applicationId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.ListDocumentsAsync(applicationId, ct)));

    [HttpGet("{applicationId:guid}/documents/{documentId:guid}/download")]
    public Task<IActionResult> Download(Guid applicationId, Guid documentId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await service.DownloadAsync(applicationId, documentId, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers.CacheControl = "no-store";
            return File(result.Content, result.ContentType, result.FileName);
        });

    [HttpPut("{applicationId:guid}/documents/{documentId:guid}/review")]
    public Task<IActionResult> Review(
        Guid applicationId, Guid documentId, ReviewDocumentRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewerId)
                || reviewerId == Guid.Empty)
                return Unauthorized();

            return Ok(await service.ReviewDocumentAsync(applicationId, documentId, request, reviewerId, ct));
        });

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
        catch (ApplicationConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (IOException)
        {
            return StatusCode(503, new { message = "Document storage is currently unavailable." });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(503, new { message = "Document storage is currently unavailable." });
        }
    }
}
