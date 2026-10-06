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

    [Test]
    public async Task ReviewCanBeCorrectedWhileApplicationIsUnderReview()
    {
        await service.StartReviewAsync(store.Application.Id, default);
        await service.ReviewDocumentAsync(store.Application.Id, store.Document.Id,
            new ReviewDocumentRequest { Status = DocumentStatus.Invalid, Comment = "Check again" }, reviewer, default);
        var result = await service.ReviewDocumentAsync(store.Application.Id, store.Document.Id,
            new ReviewDocumentRequest { Status = DocumentStatus.Valid }, reviewer, default);
        Assert.That(result.Status, Is.EqualTo("VALID"));
        Assert.That(result.ReviewComment, Is.Null);
    }

    [TestCase(ApplicationStatus.Draft)]
    [TestCase(ApplicationStatus.UnderReview)]
    [TestCase(ApplicationStatus.Accepted)]
    [TestCase(ApplicationStatus.Rejected)]
    [TestCase(ApplicationStatus.Withdrawn)]
    public void OnlySubmittedStateCanStartReview(ApplicationStatus status)
    {
        SetStatus(status);
        Assert.Throws<ApplicationConflictException>(() => store.Application.StartReview());
        Assert.That(store.Application.Status, Is.EqualTo(status));
    }

    [TestCase(ApplicationStatus.Submitted)]
    [TestCase(ApplicationStatus.Accepted)]
    [TestCase(ApplicationStatus.Rejected)]
    [TestCase(ApplicationStatus.Withdrawn)]
    public void DocumentReviewRequiresUnderReviewState(ApplicationStatus status)
    {
        SetStatus(status);
        Assert.ThrowsAsync<ApplicationConflictException>(() => service.ReviewDocumentAsync(
            store.Application.Id, store.Document.Id,
            new ReviewDocumentRequest { Status = DocumentStatus.Valid }, reviewer, default));
        Assert.That(store.Document.Status, Is.EqualTo(DocumentStatus.Pending));
        Assert.That(store.Saves, Is.Zero);
    }

    [Test]
    public void InvalidReviewDoesNotMutateDocument()
    {
        Assert.Throws<ArgumentException>(() => store.Document.Review(DocumentStatus.Pending, null, reviewer));
        Assert.Throws<ArgumentException>(() => store.Document.Review((DocumentStatus)999, null, reviewer));
        Assert.Throws<ArgumentException>(() => store.Document.Review(DocumentStatus.Invalid, "  ", reviewer));
        Assert.Throws<ArgumentException>(() => store.Document.Review(DocumentStatus.Valid, new string('x', 2001), reviewer));
        Assert.Throws<ArgumentException>(() => store.Document.Review(DocumentStatus.Valid, null, Guid.Empty));
        Assert.That(store.Document.Status, Is.EqualTo(DocumentStatus.Pending));
        Assert.That(store.Document.ReviewedAtUtc, Is.Null);
    }

    [Test]
    public async Task DocumentFromOtherApplicationIsHidden()
    {
        await service.StartReviewAsync(store.Application.Id, default);
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReviewDocumentAsync(
            store.Application.Id, Guid.NewGuid(),
            new ReviewDocumentRequest { Status = DocumentStatus.Valid }, reviewer, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.DownloadAsync(store.Application.Id, Guid.NewGuid(), default));
    }

    [Test]
    public void DraftIsHiddenFromStaff()
    {
        store.Application = new StudentApplication(Guid.NewGuid(), Guid.NewGuid());
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(store.Application.Id, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.StartReviewAsync(store.Application.Id, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListDocumentsAsync(store.Application.Id, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.DownloadAsync(store.Application.Id, store.Document.Id, default));
    }

    [Test]
    public async Task StaffCanListAndDownloadSubmittedDocuments()
    {
        Assert.That(await service.ListDocumentsAsync(store.Application.Id, default), Has.Count.EqualTo(1));
        var download = await service.DownloadAsync(store.Application.Id, store.Document.Id, default);
        await using var stream = download.Content;
        Assert.That(download.FileName, Is.EqualTo("document.pdf"));
        Assert.That(stream.Length, Is.EqualTo(5));
    }

    [Test]
    public void InvalidFiltersAreRejected()
    {
        Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(null, null, 0, default));
        Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(null, null, int.MaxValue, default));
        Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(null, ApplicationStatus.Draft, 1, default));
        Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(null, (ApplicationStatus)999, 1, default));
    }

    [Test]
    public async Task ControllerMapsMissingConflictAndBadRequest()
    {
        var controller = new ApplicationReviewsController(service);
        Assert.That(await controller.Get(Guid.NewGuid(), default), Is.TypeOf<NotFoundObjectResult>());
        Assert.That(await controller.List(default, page: 0), Is.TypeOf<BadRequestObjectResult>());
        await service.StartReviewAsync(store.Application.Id, default);
        Assert.That(await controller.StartReview(store.Application.Id, default), Is.TypeOf<ConflictObjectResult>());
    }

    [TestCase("STUDENT", false, false)]
    [TestCase("STUDENT", true, false)]
    [TestCase("STAFF", false, false)]
    [TestCase("ADMIN", false, false)]
    [TestCase("STAFF", true, true)]
    [TestCase("ADMIN", true, true)]
    public async Task StaffRoutesRequireRoleAndPermission(string role, bool permission, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy("ManageApplications", policy =>
            policy.RequireAuthenticatedUser().RequireClaim("permission", "ManageApplications")));
        using var provider = services.BuildServiceProvider();
        var metadata = typeof(ApplicationReviewsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (permission)
            claims.Add(new Claim("permission", "ManageApplications"));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy!);
        Assert.That(result.Succeeded, Is.EqualTo(allowed));
    }

    private void SetStatus(ApplicationStatus status) =>
        typeof(StudentApplication).GetProperty(nameof(StudentApplication.Status))!.SetValue(store.Application, status);

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
