using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using StudentCenter.ApplicationService.API.Controllers;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Domain.Services;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Tests;

public sealed class PreliminaryRankingTests
{
    private Store store = null!;
    private PreliminaryRankingService service = null!;
    private readonly Guid staffId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        store = new Store();
        service = new PreliminaryRankingService(store, store);
    }

    private RankingCandidate AddCandidate(decimal points)
    {
        var application = new StudentApplication(store.Competition.Id, Guid.NewGuid());
        application.Submit();
        application.StartReview();
        var document = new ApplicationDocument(application.Id, DocumentType.Transcript,
            "test.pdf", "reference", "application/pdf", 5);
        document.Review(DocumentStatus.Valid, null, staffId);
        var score = new ScoringResult(application.Id);
        score.Calculate(points, 0, 0, 0, 0, staffId, DocumentReviewSnapshot.Fingerprint([document]));
        var candidate = new RankingCandidate(application, score, new[] { document });
        store.Candidates.Add(candidate);
        return candidate;
    }

    private Task<StudentCenter.ApplicationService.Application.DTOs.RankingResponse> Generate(
        RankingTieRule rule = RankingTieRule.SharedPosition) =>
        service.GenerateAsync(store.Competition.Id, rule, staffId, default);

    [Test]
    public async Task SortsDescendingAndEqualScoresSharePosition()
    {
        AddCandidate(50);
        AddCandidate(80);
        AddCandidate(80);
        var result = await Generate();
        Assert.That(result.Status, Is.EqualTo("DRAFT"));
        Assert.That(result.Entries.Select(entry => entry.TotalPoints), Is.EqualTo(new[] { 80m, 80m, 50m }));
        Assert.That(result.Entries.Select(entry => entry.Position), Is.EqualTo(new[] { 1, 1, 3 }));
    }

    [Test]
    public async Task ChangedScoresBlockPublishUntilDraftRegenerated()
    {
        var candidate = AddCandidate(70);
        var first = await Generate();
        candidate.Score!.Calculate(90, 0, 0, 0, 0, staffId, DocumentReviewSnapshot.Fingerprint(candidate.Documents));
        Assert.ThrowsAsync<ApplicationConflictException>(() => service.PublishAsync(store.Competition.Id, staffId, default));
        var rebuilt = await Generate();
        Assert.That(rebuilt.Id, Is.EqualTo(first.Id));
        Assert.That(store.Adds, Is.EqualTo(1));
        Assert.That(rebuilt.Entries, Has.Count.EqualTo(1));
        Assert.That((await service.PublishAsync(store.Competition.Id, staffId, default)).Status, Is.EqualTo("PUBLISHED"));
    }

    private sealed class Store : IRankingRepository, IStudentClient
    {
        public Competition Competition { get; } = new("2026/27", "Test", null,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
        public Ranking? Ranking { get; private set; }
        public List<RankingCandidate> Candidates { get; } = [];
        public Guid StudentId { get; set; } = Guid.NewGuid();
        public int Adds { get; private set; }

        public Store()
        {
            Competition.Open();
            Competition.Close();
        }

        public Task<Competition?> GetCompetitionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Competition?>(id == Competition.Id ? Competition : null);

        public Task<Ranking?> GetPreliminaryAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Ranking?.CompetitionId == id ? Ranking : null);

        public Task<IReadOnlyCollection<RankingCandidate>> GetCandidatesAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<RankingCandidate>>(Candidates.ToArray());

        public Task AddAsync(Ranking ranking, CancellationToken ct)
        {
            Ranking = ranking;
            Adds++;
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);

        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct = default) => Task.FromResult(StudentId);
    }
}
