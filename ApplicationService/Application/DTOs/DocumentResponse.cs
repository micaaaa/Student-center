namespace StudentCenter.ApplicationService.Application.DTOs;

public sealed record DocumentResponse(
    Guid Id,
    Guid ApplicationId,
    string DocumentType,
    string FileName,
    string ContentType,
    long Size,
    string Status,
    DateTime UploadedAtUtc,
    DateTime? ReviewedAtUtc,
    Guid? ReviewedByUserId,
    string? ReviewComment);
public sealed record DocumentDownload(Stream Content, string ContentType, string FileName);
