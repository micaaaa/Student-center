using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.API.Controllers;

public sealed class UploadDocumentRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [EnumDataType(typeof(DocumentType))]
    public DocumentType DocumentType { get; set; }
}

[ApiController]
[Route("api/applications/{applicationId:guid}/documents")]
[Authorize(Roles = "STUDENT")]
public sealed class ApplicationDocumentsController(DocumentService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(Guid applicationId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.ListAsync(applicationId, ct)));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DocumentService.MaxFileSize + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = DocumentService.MaxFileSize + 64 * 1024)]
    public Task<IActionResult> Upload(Guid applicationId, [FromForm] UploadDocumentRequest request, CancellationToken ct) =>
        ExecuteAsync(
        async () =>
    {
        if (request.File.Length > DocumentService.MaxFileSize)
            throw new DocumentTooLargeException();
        await using var content = request.File.OpenReadStream();
        var result = await service.UploadAsync(applicationId, request.DocumentType, request.File.FileName, content, ct);
        return CreatedAtAction(nameof(Download), new { applicationId, documentId = result.Id }, result);
    });

    [HttpGet("{documentId:guid}/download")]
    public Task<IActionResult> Download(Guid applicationId, Guid documentId, CancellationToken ct) => ExecuteAsync(
        async () =>
    {
        var result = await service.DownloadAsync(applicationId, documentId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Content, result.ContentType, result.FileName);
    });

    [HttpDelete("{documentId:guid}")]
    public Task<IActionResult> Delete(Guid applicationId, Guid documentId, CancellationToken ct) => ExecuteAsync(
        async () =>
    {
        await service.DeleteAsync(applicationId, documentId, ct);
        return NoContent();
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
        catch (DocumentTooLargeException exception)
        {
            return StatusCode(413, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { message = "Student service is currently unavailable." });
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(503, new { message = "Request timed out." });
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
