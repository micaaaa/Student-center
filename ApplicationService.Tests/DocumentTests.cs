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

    [TestCase("list")]
    [TestCase("upload")]
    [TestCase("download")]
    [TestCase("delete")]
    public async Task ForeignOwnerCannotAccessDocuments(string operation)
    {
        var document = await Upload();
        fixture.StudentId = Guid.NewGuid();
        Assert.ThrowsAsync<KeyNotFoundException>(
            async () =>
        {
            switch (operation)
            {
                case "list":
                    await service.ListAsync(fixture.Application.Id, default);
                    break;
                case "upload":
                    await Upload();
                    break;
                case "download":
                    await service.DownloadAsync(fixture.Application.Id, document.Id, default);
                    break;
                case "delete":
                    await service.DeleteAsync(fixture.Application.Id, document.Id, default);
                    break;
            }
        });
        Assert.That(fixture.Files.Count, Is.EqualTo(1));
        Assert.That(fixture.Documents.Count, Is.EqualTo(1));
    }

    [TestCase(ApplicationStatus.Submitted)]
    [TestCase(ApplicationStatus.Withdrawn)]
    [TestCase(ApplicationStatus.UnderReview)]
    [TestCase(ApplicationStatus.Accepted)]
    [TestCase(ApplicationStatus.Rejected)]
    public async Task NonDraftAllowsReadingButNoChanges(ApplicationStatus status)
    {
        var document = await Upload();
        typeof(StudentApplication).GetProperty(nameof(StudentApplication.Status))!.SetValue(fixture.Application, status);
        Assert.ThrowsAsync<ApplicationConflictException>(() => Upload());
        Assert.ThrowsAsync<ApplicationConflictException>(() => service.DeleteAsync(fixture.Application.Id, document.Id, default));
        Assert.That(await service.ListAsync(fixture.Application.Id, default), Has.Count.EqualTo(1));
        var download = await service.DownloadAsync(fixture.Application.Id, document.Id, default);
        await download.Content.DisposeAsync();
    }

    [TestCase("bad.exe")]
    [TestCase("bad.txt")]
    [TestCase("")]
    public void UnsupportedNamesAreRejectedWithoutWriting(string name)
    {
        Assert.ThrowsAsync<ArgumentException>(() => Upload(name));
        Assert.That(fixture.Files, Is.Empty);
    }

    [Test]
    public void EmptyWrongSignatureAndUnknownTypeAreRejected()
    {
        Assert.ThrowsAsync<ArgumentException>(() => Upload(bytes: []));
        Assert.ThrowsAsync<ArgumentException>(() => Upload(bytes: "not a PDF"u8.ToArray()));
        Assert.ThrowsAsync<ArgumentException>(() => Upload(type: (DocumentType)999));
        Assert.That(fixture.Files, Is.Empty);
    }

    [TestCase("image.png", "image/png")]
    [TestCase("image.jpg", "image/jpeg")]
    [TestCase("image.JPEG", "image/jpeg")]
    public async Task ImageSignaturesAreAccepted(string name, string mime)
    {
        byte[] bytes = mime == "image/png" ? [137, 80, 78, 71, 13, 10, 26, 10, 0] : [255, 216, 255, 0];
        Assert.That((await Upload(name, bytes)).ContentType, Is.EqualTo(mime));
    }

    [Test]
    public async Task SizeLimitIsEnforcedAgainstActualBytes()
    {
        var bytes = new byte[DocumentService.MaxFileSize + 1];
        Pdf.CopyTo(bytes, 0);
        Assert.ThrowsAsync<DocumentTooLargeException>(() => Upload(bytes: bytes));
        Assert.That(fixture.Files, Is.Empty);
        Assert.That(
            (await Upload(bytes: bytes[..DocumentService.MaxFileSize])).Size,
            Is.EqualTo(DocumentService.MaxFileSize));
    }

    [Test]
    public void DatabaseFailureCleansUploadedFile()
    {
        fixture.FailAdd = true;
        Assert.ThrowsAsync<InvalidOperationException>(() => Upload());
        Assert.That(fixture.Files, Is.Empty);
    }

    [Test]
    public async Task FailedDatabaseDeleteKeepsFile()
    {
        var uploaded = await Upload();
        fixture.FailRemove = true;
        Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(fixture.Application.Id, uploaded.Id, default));
        Assert.That(fixture.Files.Count, Is.EqualTo(1));
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

    [Test]
    public async Task MissingPhysicalFileReturnsNotFound()
    {
        var document = await Upload();
        fixture.Files.Clear();
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.DownloadAsync(fixture.Application.Id, document.Id, default));
    }

    private sealed class Fixture : IApplicationRepository, IStudentClient, IDocumentRepository, IDocumentStorage
    {
        public Guid StudentId = Guid.NewGuid();

        public StudentApplication Application { get; }
        public List<ApplicationDocument> Documents { get; } = [];
        public Dictionary<string, byte[]> Files { get; } = [];

        public bool FailAdd, FailRemove;

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
            if (FailRemove)
                throw new InvalidOperationException();
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
