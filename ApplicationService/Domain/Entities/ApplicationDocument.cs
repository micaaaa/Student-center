using StudentCenter.ApplicationService.Domain.Enums;

namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class ApplicationDocument
{
    private ApplicationDocument()
    {
    }

    public ApplicationDocument(
        Guid applicationId,
        DocumentType type,
        string fileName,
        string fileReference,
        string contentType,
        long size)
    {
        Id = Guid.NewGuid();
        ApplicationId = applicationId;
        DocumentType = type;
        FileName = fileName;
        FileReference = fileReference;
        ContentType = contentType;
        Size = size;
        UploadedAtUtc = DateTime.UtcNow;
        Status = DocumentStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string FileName { get; private set; } = null!;
    public string FileReference { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long Size { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewComment { get; private set; }

    public void Review(DocumentStatus status, string? comment, Guid reviewerId)
    {
        if (status is not (DocumentStatus.Valid or DocumentStatus.Invalid))
            throw new ArgumentException("Review status must be VALID or INVALID.");
        if (reviewerId == Guid.Empty)
            throw new ArgumentException("A reviewer is required.");

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (status == DocumentStatus.Invalid && normalizedComment is null)
            throw new ArgumentException("A comment is required for an invalid document.");
        if (normalizedComment?.Length > 2000)
            throw new ArgumentException("Review comment must not exceed 2000 characters.");

        Status = status;
        ReviewComment = normalizedComment;
        ReviewedByUserId = reviewerId;
        ReviewedAtUtc = DateTime.UtcNow;
    }
}
