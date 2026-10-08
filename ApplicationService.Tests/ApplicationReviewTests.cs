using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using StudentCenter.ApplicationService.API.Controllers;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Tests;

public sealed class ApplicationReviewTests
{
    private Store store = null!;
    private ApplicationReviewService service = null!;
    private readonly Guid reviewer = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        store = new Store();
        store.Application.Submit();
        service = new ApplicationReviewService(store, store, store, store);
    }

    [Test]
    public async Task SubmittedApplicationCanEnterReviewAndDocumentCanBeReviewed()
    {
        Assert.That((await service.StartReviewAsync(store.Application.Id, default)).Status, Is.EqualTo("UNDER_REVIEW"));
        var result = await service.ReviewDocumentAsync(
            store.Application.Id, store.Document.Id,
            new ReviewDocumentRequest { Status = DocumentStatus.Invalid, Comment = " Unreadable scan " }, reviewer, default);

        Assert.That(result.Status, Is.EqualTo("INVALID"));
        Assert.That(result.ReviewComment, Is.EqualTo("Unreadable scan"));
        Assert.That(result.ReviewedByUserId, Is.EqualTo(reviewer));
        Assert.That(result.ReviewedAtUtc, Is.Not.Null);
        Assert.That(store.Saves, Is.EqualTo(2));
        Assert.Throws<ApplicationConflictException>(() => store.Application.Withdraw());
    }

    private sealed class Store : IApplicationRepository, IApplicationReviewRepository, IDocumentRepository, IDocumentStorage
    {
        public StudentApplication Application { get; set; } = new(Guid.NewGuid(), Guid.NewGuid());
        public ApplicationDocument Document { get; }
        public int Saves { get; private set; }

        public Store()
        {
            Document = new ApplicationDocument(Application.Id, DocumentType.Transcript,
                "document.pdf", "reference", "application/pdf", 5);
        }

        public Task<StudentApplication?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<StudentApplication?>(id == Application.Id ? Application : null);

        public Task SaveAsync(CancellationToken ct = default)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public Task<ApplicationDocument?> FindAsync(Guid applicationId, Guid documentId, CancellationToken ct) =>
            Task.FromResult<ApplicationDocument?>(Document.ApplicationId == applicationId && Document.Id == documentId ? Document : null);

        public Task<IReadOnlyCollection<ApplicationDocument>> ListAsync(Guid applicationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<ApplicationDocument>>([Document]);

        public Task<Stream> OpenAsync(string reference, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream("%PDF-"u8.ToArray()));

        public Task<IReadOnlyCollection<StudentApplication>> ListAsync(
            Guid? competitionId, ApplicationStatus? status, int page, CancellationToken ct, Guid? studentId = null) =>
            Task.FromResult<IReadOnlyCollection<StudentApplication>>([Application]);

        public Task<StudentApplication?> GetForStudentAsync(Guid c, Guid s, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyCollection<StudentApplication>> GetMineAsync(Guid s, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task AddAsync(StudentApplication application, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task AddAsync(ApplicationDocument document, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task RemoveAsync(ApplicationDocument document, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<string> SaveAsync(byte[] bytes, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task DeleteAsync(string reference, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
