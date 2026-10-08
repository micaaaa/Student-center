using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

public sealed class ApplicationScoringTests
{
    private Store store = null!;
    private ApplicationScoringService service = null!;
    private readonly Guid reviewer = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        store = new Store();
        store.Application.Submit();
        store.Application.StartReview();
        store.Documents[0].Review(DocumentStatus.Valid, null, reviewer);
        service = new ApplicationScoringService(store, store, store, store);
    }

    private static CalculateScoreRequest Request(decimal academic = 40.25m) => new()
    {
        AcademicPoints = academic,
        IncomePoints = 12.5m,
        ECTSPoints = 10,
        StudyYearPoints = 5,
        AdditionalPoints = 2.25m
    };

    [Test]
    public async Task CalculatesExactTotalAndStudentCanReadBreakdown()
    {
        var result = await service.CalculateAsync(store.Application.Id, Request(), reviewer, default);
        Assert.That(result.TotalPoints, Is.EqualTo(70m));
        Assert.That(result.AcademicPoints, Is.EqualTo(40.25m));
        Assert.That(result.CalculatedByUserId, Is.EqualTo(reviewer));
        Assert.That(result.CalculatedAtUtc, Is.Not.EqualTo(default(DateTime)));
        Assert.That(result.IsCurrent, Is.True);
        Assert.That((await service.GetMineAsync(store.Application.Id, default)).TotalPoints, Is.EqualTo(70m));
        Assert.That(store.Application.Status, Is.EqualTo(ApplicationStatus.UnderReview));
    }

    [TestCase(DocumentStatus.Pending)]
    public void AllDocumentsMustBeValid(DocumentStatus status)
    {
        var document = new ApplicationDocument(store.Application.Id, DocumentType.Other, "other.pdf", "other", "application/pdf", 1);
        if (status == DocumentStatus.Invalid)
            document.Review(status, "Unreadable", reviewer);
        store.Documents.Add(document);
        Assert.ThrowsAsync<ApplicationConflictException>(() =>
            service.CalculateAsync(store.Application.Id, Request(), reviewer, default));
        Assert.That(store.Saves, Is.Zero);
    }

    [Test]
    public async Task ChangedReviewMakesResultStaleUntilRecalculated()
    {
        await service.CalculateAsync(store.Application.Id, Request(), reviewer, default);
        store.Documents[0].Review(DocumentStatus.Invalid, "Recheck", reviewer);
        Assert.That((await service.GetMineAsync(store.Application.Id, default)).IsCurrent, Is.False);
        store.Documents[0].Review(DocumentStatus.Valid, "Corrected", reviewer);
        // Ensure a distinct review snapshot without relying on wall-clock precision.
        typeof(ApplicationDocument).GetProperty(nameof(ApplicationDocument.ReviewedAtUtc))!
            .SetValue(store.Documents[0], DateTime.UtcNow.AddMinutes(1));
        Assert.That((await service.GetForStaffAsync(store.Application.Id, default)).IsCurrent, Is.False);
        Assert.That((await service.CalculateAsync(store.Application.Id, Request(), reviewer, default)).IsCurrent, Is.True);
    }

    private sealed class Store : IApplicationRepository, IDocumentRepository, IScoringRepository, IStudentClient
    {
        public Guid StudentId { get; set; } = Guid.NewGuid();
        public StudentApplication Application { get; }
        public List<ApplicationDocument> Documents { get; } = [];
        public ScoringResult? Score { get; private set; }
        public int Adds { get; private set; }
        public int Saves { get; private set; }

        public Store()
        {
            Application = new StudentApplication(Guid.NewGuid(), StudentId);
            Documents.Add(new ApplicationDocument(Application.Id, DocumentType.Transcript,
                "test.pdf", "reference", "application/pdf", 1));
        }

        public Task<StudentApplication?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<StudentApplication?>(Application.Id == id ? Application : null);

        public Task<IReadOnlyCollection<ApplicationDocument>> ListAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<ApplicationDocument>>(Documents.ToArray());

        public Task<ScoringResult?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Score);

        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct = default) => Task.FromResult(StudentId);

        public Task AddAsync(ScoringResult score, CancellationToken ct)
        {
            Score = score;
            Adds++;
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public Task<StudentApplication?> GetForStudentAsync(Guid c, Guid s, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<StudentApplication>> GetMineAsync(Guid s, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(StudentApplication application, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ApplicationDocument?> FindAsync(Guid a, Guid d, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task AddAsync(ApplicationDocument document, CancellationToken ct) => throw new NotSupportedException();

        public Task RemoveAsync(ApplicationDocument document, CancellationToken ct) => throw new NotSupportedException();
    }
}
