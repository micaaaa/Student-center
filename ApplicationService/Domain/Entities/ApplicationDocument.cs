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
}
