using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Application.Interfaces;

public interface IDocumentRepository
{
    Task<IReadOnlyCollection<ApplicationDocument>> ListAsync(Guid applicationId, CancellationToken ct);

    Task<ApplicationDocument?> FindAsync(Guid applicationId, Guid documentId, CancellationToken ct);

    Task AddAsync(ApplicationDocument document, CancellationToken ct);

    Task RemoveAsync(ApplicationDocument document, CancellationToken ct);
}

public interface IDocumentStorage
{
    Task<string> SaveAsync(byte[] content, CancellationToken ct);

    Task<Stream> OpenAsync(string reference, CancellationToken ct);

    Task DeleteAsync(string reference, CancellationToken ct);
}
