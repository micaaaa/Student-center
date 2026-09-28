using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Infrastructure.Repositories;

public sealed class DocumentRepository(ApplicationDbContext db) : IDocumentRepository
{
    public async Task<IReadOnlyCollection<ApplicationDocument>> ListAsync(Guid applicationId, CancellationToken ct) =>
        await db.Documents.AsNoTracking().Where(x => x.ApplicationId == applicationId).OrderByDescending(x => x.UploadedAtUtc)
        .ThenBy(x => x.Id)
        .ToArrayAsync(ct);

    public Task<ApplicationDocument?> FindAsync(Guid applicationId, Guid documentId, CancellationToken ct) =>
        db.Documents.SingleOrDefaultAsync(x => x.ApplicationId == applicationId && x.Id == documentId, ct);

    public async Task AddAsync(ApplicationDocument document, CancellationToken ct)
    {
        db.Documents.Add(document);
        await StudentMutationWriter.SaveAsync(db, ct);
    }

    public async Task RemoveAsync(ApplicationDocument document, CancellationToken ct)
    {
        db.Documents.Remove(document);
        await StudentMutationWriter.SaveAsync(db, ct);
    }
}
