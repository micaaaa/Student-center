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
    public async Task EarlierSubmissionCanResolveTiesWhenSelected()
    {
        var later = AddCandidate(80);
        var earlier = AddCandidate(80);
        typeof(StudentApplication).GetProperty(nameof(StudentApplication.SubmittedAtUtc))!
            .SetValue(earlier.Application, DateTime.UtcNow.AddDays(-1));
        var result = await Generate(RankingTieRule.EarlierSubmission);
        Assert.That(result.Entries.First().ApplicationId, Is.EqualTo(earlier.Application.Id));
        Assert.That(result.Entries.Select(entry => entry.Position), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(result.Entries.Last().ApplicationId, Is.EqualTo(later.Application.Id));
    }

    [Test]
    public async Task UnpublishedListIsHiddenAndPublishedListMarksOwnEntry()
    {
        var own = AddCandidate(70);
        AddCandidate(80);
        store.StudentId = own.Application.StudentId;
        await Generate();
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetPublishedAsync(store.Competition.Id, default));
        await service.PublishAsync(store.Competition.Id, staffId, default);
        var result = await service.GetPublishedAsync(store.Competition.Id, default);
        Assert.That(result.PublishedAtUtc, Is.Not.Null);
        Assert.That(result.Entries.Count(entry => entry.IsMine), Is.EqualTo(1));
        Assert.That(result.Entries.Single(entry => entry.IsMine).ApplicationId, Is.EqualTo(own.Application.Id));
        Assert.That(result.Entries.First().GetType().GetProperty("StudentId"), Is.Null);
    }

    [Test]
    public async Task PublishedListCannotBeRegeneratedOrRepublishedAndKeepsSnapshot()
    {
        var candidate = AddCandidate(70);
        await Generate();
        await service.PublishAsync(store.Competition.Id, staffId, default);
        candidate.Score!.Calculate(90, 0, 0, 0, 0, staffId, DocumentReviewSnapshot.Fingerprint(candidate.Documents));
        Assert.ThrowsAsync<ApplicationConflictException>(() => Generate());
        Assert.ThrowsAsync<ApplicationConflictException>(() => service.PublishAsync(store.Competition.Id, staffId, default));
        Assert.That((await service.GetPublishedAsync(store.Competition.Id, default)).Entries.Single().TotalPoints, Is.EqualTo(70));
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

    [TestCase("unscored")]
    [TestCase("submitted")]
    [TestCase("invalid")]
    [TestCase("stale")]
    public void IncompleteApplicationBlocksWholeList(string reason)
    {
        AddCandidate(90);
        var candidate = AddCandidate(70);
        switch (reason)
        {
            case "unscored":
                store.Candidates[1] = candidate with { Score = null };
                break;
            case "submitted":
                typeof(StudentApplication).GetProperty(nameof(StudentApplication.Status))!
                    .SetValue(candidate.Application, ApplicationStatus.Submitted);
                break;
            case "invalid":
                candidate.Documents.Single().Review(DocumentStatus.Invalid, "Unreadable", staffId);
                break;
            case "stale":
                typeof(ApplicationDocument).GetProperty(nameof(ApplicationDocument.ReviewedAtUtc))!
                    .SetValue(candidate.Documents.Single(), DateTime.UtcNow.AddMinutes(1));
                break;
        }
        Assert.ThrowsAsync<ApplicationConflictException>(() => Generate());
        Assert.That(store.Ranking, Is.Null);
    }

    [Test]
    public async Task WithdrawnDraftAndRejectedApplicationsAreExcluded()
    {
        var active = AddCandidate(70);
        foreach (var status in new[] { ApplicationStatus.Withdrawn, ApplicationStatus.Draft, ApplicationStatus.Rejected })
        {
            var candidate = AddCandidate(90);
            typeof(StudentApplication).GetProperty(nameof(StudentApplication.Status))!.SetValue(candidate.Application, status);
        }
        var result = await Generate();
        Assert.That(result.Entries.Single().ApplicationId, Is.EqualTo(active.Application.Id));
    }

    [TestCase(CompetitionStatus.Draft)]
    [TestCase(CompetitionStatus.Open)]
    [TestCase(CompetitionStatus.Finalized)]
    public void CompetitionMustBeClosed(CompetitionStatus status)
    {
        AddCandidate(70);
        typeof(Competition).GetProperty(nameof(Competition.Status))!.SetValue(store.Competition, status);
        Assert.ThrowsAsync<ApplicationConflictException>(() => Generate());
    }

    [Test]
    public void EmptyCompetitionCannotProduceRanking() =>
        Assert.ThrowsAsync<ApplicationConflictException>(() => Generate());

    [Test]
    public async Task NewCandidateAfterGenerationRequiresRegeneration()
    {
        AddCandidate(70);
        await Generate();
        AddCandidate(80);
        Assert.ThrowsAsync<ApplicationConflictException>(() => service.PublishAsync(store.Competition.Id, staffId, default));
    }

    [Test]
    public void MissingCompetitionAndRankingReturnNotFound()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetForStaffAsync(Guid.NewGuid(), default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateAsync(
            Guid.NewGuid(), RankingTieRule.SharedPosition, staffId, default));
    }

    [Test]
    public void InvalidTieRuleAndUserAreRejected()
    {
        AddCandidate(70);
        Assert.ThrowsAsync<ArgumentException>(() => Generate((RankingTieRule)999));
        Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAsync(
            store.Competition.Id, RankingTieRule.SharedPosition, Guid.Empty, default));
    }

    [Test]
    public void EfModelSupportsPrivateEntriesAndUniquePreliminaryRanking()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var model = db.Model.FindEntityType(typeof(Ranking))!;
        Assert.That(model.FindNavigation(nameof(Ranking.Entries))!.FieldInfo!.Name, Is.EqualTo("entries"));
        Assert.That(model.GetIndexes().Any(index => index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "CompetitionId", "Type" })), Is.True);
    }

    [Test]
    public void EfTracksRegeneratedRowsAsInsertsAndOldRowsAsDeletes()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var ranking = new Ranking(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var original = new RankingEntry(ranking.Id, applicationId, studentId, 1, 70);
        ranking.ReplaceDraft([original], "first", RankingTieRule.SharedPosition, staffId);
        db.Attach(ranking);
        var replacement = new RankingEntry(ranking.Id, applicationId, studentId, 1, 80);
        ranking.ReplaceDraft([replacement], "second", RankingTieRule.SharedPosition, staffId);
        db.ChangeTracker.DetectChanges();

        Assert.That(db.Entry(original).State, Is.EqualTo(EntityState.Deleted));
        Assert.That(db.Entry(replacement).State, Is.EqualTo(EntityState.Added));
    }

    [TestCase("STUDENT", true, false)]
    [TestCase("STAFF", false, false)]
    [TestCase("ADMIN", false, false)]
    [TestCase("STAFF", true, true)]
    [TestCase("ADMIN", true, true)]
    public async Task PublishingRequiresStaffRoleAndPermission(string role, bool permission, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy("ManageApplications", policy =>
            policy.RequireAuthenticatedUser().RequireClaim("permission", "ManageApplications")));
        using var provider = services.BuildServiceProvider();
        var metadata = typeof(PreliminaryRankingsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<IAuthorizeData>()
            .Concat(typeof(PreliminaryRankingsController).GetMethod(nameof(PreliminaryRankingsController.Publish))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<IAuthorizeData>());
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (permission)
            claims.Add(new Claim("permission", "ManageApplications"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null, policy!);
        Assert.That(result.Succeeded, Is.EqualTo(allowed));
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
