using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Tests;

public sealed class DocumentTests
{
    private Fixture fixture = null!;

    private DocumentService service = null!;

    private static readonly byte[] Pdf = "%PDF-1.7 test"u8.ToArray();

    [SetUp]
    public void SetUp()
    {
        fixture = new Fixture();
        service = new DocumentService(fixture, fixture, fixture, fixture, NullLogger<DocumentService>.Instance);
    }

    private Task<StudentCenter.ApplicationService.Application.DTOs.DocumentResponse> Upload(string name = "test.pdf", byte[]? bytes = null, DocumentType type = DocumentType.Transcript) =>
        service.UploadAsync(fixture.Application.Id, type, name, new MemoryStream(bytes ?? Pdf), default);

    [Test]
    public async Task UploadListDownloadDeletePreservesBytesAndMetadata()
    {
        var uploaded = await Upload("../../test.pdf");
        Assert.That(uploaded.FileName, Is.EqualTo("test.pdf"));
        Assert.That(uploaded.Status, Is.EqualTo("PENDING"));
        Assert.That(uploaded.Size, Is.EqualTo(Pdf.Length));
        Assert.That(uploaded.ContentType, Is.EqualTo("application/pdf"));
        Assert.That((await service.ListAsync(fixture.Application.Id, default)).Single().Id, Is.EqualTo(uploaded.Id));
        var download = await service.DownloadAsync(fixture.Application.Id, uploaded.Id, default);
        await using (download.Content)
        {
            using var copy = new MemoryStream();
            await download.Content.CopyToAsync(copy);
            Assert.That(copy.ToArray(), Is.EqualTo(Pdf));
        }

        await service.DeleteAsync(fixture.Application.Id, uploaded.Id, default);
        Assert.That(fixture.Documents, Is.Empty);
        Assert.That(fixture.Files, Is.Empty);
    }

    [Test]
    public void DatabaseFailureCleansUploadedFile()
    {
        fixture.FailAdd = true;
        Assert.ThrowsAsync<InvalidOperationException>(() => Upload());
        Assert.That(fixture.Files, Is.Empty);
    }

    [Test]
    public async Task DocumentFromAnotherApplicationCannotBeDownloadedOrDeleted()
    {
        var other = new ApplicationDocument(Guid.NewGuid(), DocumentType.Other, "other.pdf", "secret", "application/pdf", 1);
        fixture.Documents.Add(other);
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.DownloadAsync(fixture.Application.Id, other.Id, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(fixture.Application.Id, other.Id, default));
        Assert.That(await service.ListAsync(fixture.Application.Id, default), Is.Empty);
    }

    private sealed class Fixture : IApplicationRepository, IStudentClient, IDocumentRepository, IDocumentStorage
    {
        public Guid StudentId = Guid.NewGuid();

        public StudentApplication Application { get; }
        public List<ApplicationDocument> Documents { get; } = [];
        public Dictionary<string, byte[]> Files { get; } = [];

        public bool FailAdd;

        public Fixture()
        {
            Application = new StudentApplication(Guid.NewGuid(), StudentId);
        }

        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct = default) => Task.FromResult(StudentId);

        public Task<StudentApplication?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<StudentApplication?>(id == Application.Id ? Application : null);

        public Task<StudentApplication?> GetForStudentAsync(Guid c, Guid s, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<StudentApplication>> GetMineAsync(Guid s, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(StudentApplication a, CancellationToken ct = default) => throw new NotSupportedException();

        public Task SaveAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<ApplicationDocument>> ListAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<ApplicationDocument>>(Documents.Where(x => x.ApplicationId == id).ToArray());

        public Task<ApplicationDocument?> FindAsync(Guid a, Guid d, CancellationToken ct) =>
            Task.FromResult(Documents.SingleOrDefault(x => x.ApplicationId == a && x.Id == d));

        public Task AddAsync(ApplicationDocument d, CancellationToken ct)
        {
            if (FailAdd)
                throw new InvalidOperationException();
            Documents.Add(d);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(ApplicationDocument d, CancellationToken ct)
        {
            Documents.Remove(d);
            return Task.CompletedTask;
        }

        public Task<string> SaveAsync(byte[] bytes, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N");
            Files.Add(key, bytes);
            return Task.FromResult(key);
        }

        public Task<Stream> OpenAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(
            Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : throw new FileNotFoundException());

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            Files.Remove(key);
            return Task.CompletedTask;
        }
    }
}
