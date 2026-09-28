using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class DocumentService(
    IApplicationRepository applications,
    IStudentClient students,
    IDocumentRepository documents,
    IDocumentStorage storage,
    ILogger<DocumentService> logger)
{
    public const int MaxFileSize = 10 * 1024 * 1024;

    public async Task<IReadOnlyCollection<DocumentResponse>> ListAsync(Guid applicationId, CancellationToken ct)
    {
        await EnsureOwnerAsync(applicationId, false, ct);
        return (await documents.ListAsync(applicationId, ct)).Select(Map).ToArray();
    }

    public async Task<DocumentResponse> UploadAsync(Guid applicationId, DocumentType type, string fileName, Stream content, CancellationToken ct)
    {
        await EnsureOwnerAsync(applicationId, true, ct);
        if (!Enum.IsDefined(type))
            throw new ArgumentException("Unknown document type.");
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 255 || safeName.Any(char.IsControl))
            throw new ArgumentException("File name must contain between 1 and 255 characters without control characters.");
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (extension is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
            throw new ArgumentException("Allowed file formats are PDF, JPG and PNG.");
        // Read a bounded amount even when the sender supplies an incorrect Content-Length.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > MaxFileSize)
                throw new DocumentTooLargeException();
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }

        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
            throw new ArgumentException("The file is empty.");
        var contentType = ValidateSignature(extension, bytes);
        var reference = await storage.SaveAsync(bytes, ct);
        var document = new ApplicationDocument(applicationId, type, safeName, reference, contentType, bytes.Length);
        try
        {
            await documents.AddAsync(document, ct);
        }
        catch
        {
            await CleanupAsync(reference);
            throw;
        }

        return Map(document);
    }

    public async Task<DocumentDownload> DownloadAsync(Guid applicationId, Guid documentId, CancellationToken ct)
    {
        await EnsureOwnerAsync(applicationId, false, ct);
        var document = await FindAsync(applicationId, documentId, ct);
        try
        {
            return new(await storage.OpenAsync(document.FileReference, ct), document.ContentType, document.FileName);
        }
        catch (FileNotFoundException)
        {
            throw new KeyNotFoundException("Document file was not found.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new KeyNotFoundException("Document file was not found.");
        }
    }

    public async Task DeleteAsync(Guid applicationId, Guid documentId, CancellationToken ct)
    {
        await EnsureOwnerAsync(applicationId, true, ct);
        var document = await FindAsync(applicationId, documentId, ct);
        // Remove database access first; a failed file cleanup must not leave a broken accessible record.
        await documents.RemoveAsync(document, ct);
        await CleanupAsync(document.FileReference);
    }

    private async Task EnsureOwnerAsync(Guid applicationId, bool requireDraft, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        var application = await applications.GetAsync(applicationId, ct);
        if (application is null || application.StudentId != studentId)
            throw new KeyNotFoundException("Application was not found.");
        if (requireDraft && application.Status != ApplicationStatus.Draft)
            throw new ApplicationConflictException("Documents can only be changed on a draft application.");
    }

    private async Task<ApplicationDocument> FindAsync(Guid applicationId, Guid documentId, CancellationToken ct) =>
        await documents.FindAsync(applicationId, documentId, ct) ?? throw new KeyNotFoundException("Document was not found.");

    private async Task CleanupAsync(string reference)
    {
        try
        {
            await storage.DeleteAsync(reference, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Could not remove document file {Reference}; manual cleanup is required.", reference);
        }
    }

    private static string ValidateSignature(string extension, byte[] bytes)
    {
        var signature = bytes.AsSpan();
        if (extension == ".pdf" && signature.StartsWith("%PDF-"u8))
            return "application/pdf";
        if (extension is ".jpg" or ".jpeg" && signature.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
            return "image/jpeg";
        if (extension == ".png" && signature.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        throw new ArgumentException("File content does not match the selected format.");
    }

    internal static DocumentResponse Map(ApplicationDocument document) => new(
        document.Id,
        document.ApplicationId,
        document.DocumentType.ToString(),
        document.FileName,
        document.ContentType,
        document.Size,
        document.Status.ToString().ToUpperInvariant(),
        document.UploadedAtUtc,
        document.ReviewedAtUtc,
        document.ReviewedByUserId,
        document.ReviewComment);
}

public sealed class DocumentTooLargeException() : Exception("The maximum file size is 10 MB.");
