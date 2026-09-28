using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using StudentCenter.ApplicationService.API.Controllers;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Domain.Services;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Tests;

public sealed class CompetitionConclusionTests
{
    private Store store = null!;
    private ManualClock clock = null!;
    private AppealService appeals = null!;
    private CompetitionConclusionService conclusions = null!;
    private readonly Guid staff = Guid.NewGuid();

    [SetUp]
    public async Task SetUp()
    {
        clock = new ManualClock();
        store = new Store();
        AddCandidate(80);
        AddCandidate(60);
        store.StudentId = store.Candidates[0].Application.StudentId;
        appeals = new AppealService(store, store, clock);
        conclusions = new CompetitionConclusionService(store, store, clock);
        var preliminary = new PreliminaryRankingService(store, store);
        await preliminary.GenerateAsync(store.Competition.Id, RankingTieRule.SharedPosition, staff, default);
        await preliminary.PublishAsync(store.Competition.Id, staff, default);
        await Configure(1);
    }

    private Task<ConclusionSettingsResponse> Configure(int places) => conclusions.ConfigureAsync(
        store.Competition.Id, new ConclusionSettingsRequest
        {
            AvailablePlaces = places,
            AppealDeadlineUtc = store.Competition.AppealDeadlineUtc ?? clock.GetUtcNow().UtcDateTime.AddHours(1)
        }, default);

    private RankingCandidate AddCandidate(decimal points)
    {
        var application = new StudentApplication(store.Competition.Id, Guid.NewGuid());
        application.Submit();
        application.StartReview();
        var document = new ApplicationDocument(application.Id, DocumentType.Transcript,
            "test.pdf", "reference", "application/pdf", 5);
        document.Review(DocumentStatus.Valid, null, staff);
        var score = new ScoringResult(application.Id);
        score.Calculate(points, 0, 0, 0, 0, staff, DocumentReviewSnapshot.Fingerprint([document]));
        SetProperty(score, nameof(ScoringResult.CalculatedAtUtc), clock.GetUtcNow().UtcDateTime.AddMinutes(-1));
        var candidate = new RankingCandidate(application, score, new[] { document });
        store.Candidates.Add(candidate);
        return candidate;
    }

    private void Recalculate(RankingCandidate candidate, decimal points)
    {
        candidate.Score!.Calculate(points, 0, 0, 0, 0, staff, DocumentReviewSnapshot.Fingerprint(candidate.Documents));
        SetProperty(candidate.Score, nameof(ScoringResult.CalculatedAtUtc), clock.GetUtcNow().UtcDateTime.AddSeconds(1));
    }

    private void AfterDeadline() => clock.Now = new DateTimeOffset(store.Competition.AppealDeadlineUtc!.Value.AddSeconds(1));

    [Test]
    public async Task FullAcceptedAppealFinalRankingAndEligibilityFlow()
    {
        var candidate = store.Candidates[1];
        store.StudentId = candidate.Application.StudentId;
        var appeal = await appeals.SubmitAsync(candidate.Application.Id, "Incorrect points", default);
        Assert.That(appeal.Status, Is.EqualTo("SUBMITTED"));
        Assert.That((await appeals.GetMineAsync(candidate.Application.Id, default)).Id, Is.EqualTo(appeal.Id));
        await appeals.StartReviewAsync(appeal.Id, default);
        Recalculate(candidate, 90);
        var resolution = await appeals.ResolveAsync(appeal.Id,
            new ResolveAppealRequest { Accepted = true, Response = "Points corrected" }, staff, default);
        Assert.That(resolution.Status, Is.EqualTo("ACCEPTED"));
        Assert.That(resolution.ResolvedByUserId, Is.EqualTo(staff));
        Assert.That(resolution.ResolvedAtUtc, Is.EqualTo(clock.GetUtcNow().UtcDateTime));
        AfterDeadline();
        var draft = await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        Assert.That(draft.Entries.First().ApplicationId, Is.EqualTo(candidate.Application.Id));
        Assert.That(draft.Entries.First().Eligible, Is.True);
        Assert.ThrowsAsync<KeyNotFoundException>(() => conclusions.GetPublishedAsync(store.Competition.Id, default));
        Assert.That(store.Decisions, Is.Empty);
        Assert.That(store.Events, Is.Empty);

        var result = await conclusions.PublishAsync(store.Competition.Id, staff, default);
        Assert.That(result.Status, Is.EqualTo("PUBLISHED"));
        Assert.That(store.Competition.Status, Is.EqualTo(CompetitionStatus.Finalized));
        Assert.That(store.Decisions.Count, Is.EqualTo(2));
        Assert.That(store.Decisions.Count(decision => decision.Eligible), Is.EqualTo(1));
        Assert.That(candidate.Application.Status, Is.EqualTo(ApplicationStatus.Accepted));
        Assert.That(store.Candidates[0].Application.Status, Is.EqualTo(ApplicationStatus.Rejected));
        var decision = await conclusions.GetMyEligibilityAsync(store.Competition.Id, default);
        Assert.That(decision.Eligible, Is.True);
        Assert.That(decision.StudentId, Is.EqualTo(store.StudentId));
        var published = await conclusions.GetPublishedAsync(store.Competition.Id, default);
        Assert.That(published.Entries.Single(entry => entry.IsMine).Eligible, Is.True);
        Assert.That(published.Entries.First().GetType().GetProperty("StudentId"), Is.Null);
        Assert.That(store.Events, Has.Count.EqualTo(1));
        var message = store.Events.Single();
        var payload = JsonSerializer.Deserialize<AccommodationEligibilityGranted>(message.Payload)!;
        Assert.That(payload.StudentId, Is.EqualTo(candidate.Application.StudentId));
        Assert.That(payload.EligibilityId, Is.EqualTo(decision.Id));
        Assert.That(message.PublishedAtUtc, Is.Null);
        Assert.That(store.Preliminary!.Entries.Single(entry => entry.ApplicationId == candidate.Application.Id).TotalPoints,
            Is.EqualTo(60));
    }

    [Test]
    public async Task RejectedAppealRequiresResponseAndPreservesPoints()
    {
        var application = store.Candidates[0].Application;
        var appeal = await appeals.SubmitAsync(application.Id, "Recheck", default);
        await appeals.StartReviewAsync(appeal.Id, default);
        Assert.ThrowsAsync<ArgumentException>(() => appeals.ResolveAsync(appeal.Id,
            new ResolveAppealRequest { Accepted = false, Response = " " }, staff, default));
        var resolved = await appeals.ResolveAsync(appeal.Id,
            new ResolveAppealRequest { Accepted = false, Response = "Initial scoring is correct" }, staff, default);
        Assert.That(resolved.Status, Is.EqualTo("REJECTED"));
        Assert.That(store.Candidates[0].Score!.TotalPoints, Is.EqualTo(80));
        AfterDeadline();
        Assert.That((await conclusions.GenerateAsync(store.Competition.Id, staff, default)).Entries, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AcceptedAppealRequiresNewCurrentScoring()
    {
        var candidate = store.Candidates[0];
        var appeal = await appeals.SubmitAsync(candidate.Application.Id, "Recheck", default);
        await appeals.StartReviewAsync(appeal.Id, default);
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.ResolveAsync(appeal.Id,
            new ResolveAppealRequest { Accepted = true, Response = "Accepted" }, staff, default));
        Recalculate(candidate, 85);
        candidate.Documents.Single().Review(DocumentStatus.Invalid, "Unreadable", staff);
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.ResolveAsync(appeal.Id,
            new ResolveAppealRequest { Accepted = true, Response = "Accepted" }, staff, default));
        Assert.That(store.Appeals.Single().Status, Is.EqualTo(AppealStatus.UnderReview));
    }

    [Test]
    public async Task ForeignStudentCannotSubmitOrReadAppeal()
    {
        var application = store.Candidates[0].Application;
        await appeals.SubmitAsync(application.Id, "Recheck", default);
        store.StudentId = Guid.NewGuid();
        Assert.ThrowsAsync<KeyNotFoundException>(() => appeals.SubmitAsync(application.Id, "Recheck", default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => appeals.GetMineAsync(application.Id, default));
    }

    [Test]
    public async Task DuplicateAppealIsRejected()
    {
        var application = store.Candidates[0].Application;
        await appeals.SubmitAsync(application.Id, "Recheck", default);
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.SubmitAsync(application.Id, "Again", default));
        Assert.That(store.Appeals, Has.Count.EqualTo(1));
    }

    [Test]
    public void AppealAfterDeadlineIsRejected()
    {
        AfterDeadline();
        Assert.ThrowsAsync<ApplicationConflictException>(() =>
            appeals.SubmitAsync(store.Candidates[0].Application.Id, "Late appeal", default));
    }

    [Test]
    public async Task AppealAtExactDeadlineIsAllowedButFinalizationIsNot()
    {
        clock.Now = new DateTimeOffset(store.Competition.AppealDeadlineUtc!.Value);
        await appeals.SubmitAsync(store.Candidates[0].Application.Id, "At deadline", default);
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.GenerateAsync(store.Competition.Id, staff, default));
    }

    [Test]
    public void MissingPreliminaryAndConfigurationBlockAppeals()
    {
        store.Preliminary = null;
        Assert.ThrowsAsync<ApplicationConflictException>(() =>
            appeals.SubmitAsync(store.Candidates[0].Application.Id, "Recheck", default));
    }

    [Test]
    public void AppealRequiresPresenceOnPreliminaryRanking()
    {
        var candidate = AddCandidate(50);
        store.StudentId = candidate.Application.StudentId;
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.SubmitAsync(candidate.Application.Id, "Recheck", default));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnresolvedAppealBlocksFinalRanking(bool startReview)
    {
        var appeal = await appeals.SubmitAsync(store.Candidates[0].Application.Id, "Recheck", default);
        if (startReview)
            await appeals.StartReviewAsync(appeal.Id, default);
        AfterDeadline();
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.GenerateAsync(store.Competition.Id, staff, default));
    }

    [Test]
    public async Task AppealCannotBeResolvedWithoutReviewOrResolvedTwice()
    {
        var appeal = await appeals.SubmitAsync(store.Candidates[0].Application.Id, "Recheck", default);
        var request = new ResolveAppealRequest { Accepted = false, Response = "Checked" };
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.ResolveAsync(appeal.Id, request, staff, default));
        await appeals.StartReviewAsync(appeal.Id, default);
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.StartReviewAsync(appeal.Id, default));
        await appeals.ResolveAsync(appeal.Id, request, staff, default);
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.ResolveAsync(appeal.Id, request, staff, default));
    }

    [Test]
    public async Task SharedTieAtCapacityBoundaryBlocksUntilCapacityAdjusted()
    {
        Recalculate(store.Candidates[1], 80);
        AfterDeadline();
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.GenerateAsync(store.Competition.Id, staff, default));
        Assert.That(store.Final, Is.Null);
        await Configure(2);
        var final = await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        Assert.That(final.Entries.All(entry => entry.Eligible), Is.True);
    }

    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(10, 2)]
    public async Task EligibilityDoesNotExceedCapacity(int capacity, int granted)
    {
        await Configure(capacity);
        AfterDeadline();
        await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        await conclusions.PublishAsync(store.Competition.Id, staff, default);
        Assert.That(store.Decisions.Count(decision => decision.Eligible), Is.EqualTo(granted));
        Assert.That(store.Events.Count, Is.EqualTo(granted));
    }

    [Test]
    public async Task ChangedCapacityOrScoresRequiresRegeneratingDraft()
    {
        AfterDeadline();
        var first = await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        await Configure(2);
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.PublishAsync(store.Competition.Id, staff, default));
        var second = await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        Assert.That(second.Id, Is.EqualTo(first.Id));
        Recalculate(store.Candidates[0], 95);
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.PublishAsync(store.Competition.Id, staff, default));
        await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        Assert.That((await conclusions.PublishAsync(store.Competition.Id, staff, default)).Status, Is.EqualTo("PUBLISHED"));
    }

    [Test]
    public async Task NewAppealAfterExtendingDeadlineBlocksExistingFinalDraft()
    {
        AfterDeadline();
        await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        await conclusions.ConfigureAsync(store.Competition.Id, new ConclusionSettingsRequest
        {
            AvailablePlaces = 1,
            AppealDeadlineUtc = clock.GetUtcNow().UtcDateTime.AddHours(1)
        }, default);
        await appeals.SubmitAsync(store.Candidates[0].Application.Id, "Within extended deadline", default);
        AfterDeadline();
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.PublishAsync(store.Competition.Id, staff, default));
    }

    [Test]
    public async Task FinalizationLocksSettingsApplicationsAndRepeatedPublication()
    {
        AfterDeadline();
        await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        await conclusions.PublishAsync(store.Competition.Id, staff, default);
        Assert.ThrowsAsync<ApplicationConflictException>(() => Configure(2));
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.GenerateAsync(store.Competition.Id, staff, default));
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.PublishAsync(store.Competition.Id, staff, default));
        Assert.ThrowsAsync<ApplicationConflictException>(() => appeals.SubmitAsync(store.Candidates[0].Application.Id, "Late", default));
        Assert.That(store.Decisions, Has.Count.EqualTo(2));
        Assert.That(store.Events, Has.Count.EqualTo(1));
        foreach (var candidate in store.Candidates)
        {
            Assert.That(candidate.Application.Status, Is.AnyOf(ApplicationStatus.Accepted, ApplicationStatus.Rejected));
            Assert.Throws<ApplicationConflictException>(() => candidate.Application.StartReview());
            Assert.Throws<ApplicationConflictException>(() => candidate.Application.Decide(true));
            Assert.Throws<ApplicationConflictException>(() => candidate.Application.Withdraw());
        }
    }

    [Test]
    public void AnnouncedDeadlineCannotBeShortened()
    {
        Assert.ThrowsAsync<ApplicationConflictException>(() => conclusions.ConfigureAsync(store.Competition.Id,
            new ConclusionSettingsRequest { AvailablePlaces = 1, AppealDeadlineUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(5) }, default));
    }

    [Test]
    public void InvalidSettingsAndAppealTextAreRejected()
    {
        Assert.ThrowsAsync<ArgumentException>(() => conclusions.ConfigureAsync(store.Competition.Id, new ConclusionSettingsRequest(), default));
        Assert.ThrowsAsync<ArgumentException>(() => Configure(-1));
        Assert.ThrowsAsync<ArgumentException>(() => appeals.SubmitAsync(store.Candidates[0].Application.Id, " ", default));
        Assert.ThrowsAsync<ArgumentException>(() => appeals.SubmitAsync(store.Candidates[0].Application.Id, new string('x', 4001), default));
    }

    [Test]
    public async Task StudentSeesOnlyOwnEligibility()
    {
        AfterDeadline();
        await conclusions.GenerateAsync(store.Competition.Id, staff, default);
        await conclusions.PublishAsync(store.Competition.Id, staff, default);
        store.StudentId = store.Candidates[1].Application.StudentId;
        Assert.That((await conclusions.GetMyEligibilityAsync(store.Competition.Id, default)).Eligible, Is.False);
        store.StudentId = Guid.NewGuid();
        Assert.ThrowsAsync<KeyNotFoundException>(() => conclusions.GetMyEligibilityAsync(store.Competition.Id, default));
    }

    [Test]
    public void UnpublishedEligibilityAndUnknownIdsReturnNotFound()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(() => conclusions.GetMyEligibilityAsync(store.Competition.Id, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => appeals.GetMineAsync(store.Candidates[0].Application.Id, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => appeals.StartReviewAsync(Guid.NewGuid(), default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => conclusions.GetSettingsAsync(Guid.NewGuid(), default));
    }

    [Test]
    public async Task ControllerMapsConflictBadRequestAndUnknownAppeal()
    {
        var controller = new CompetitionConclusionsController(conclusions)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, staff.ToString())], "test"))
                }
            }
        };
        Assert.That(await controller.Generate(store.Competition.Id, default), Is.TypeOf<ConflictObjectResult>());
        Assert.That(await controller.Configure(store.Competition.Id, new ConclusionSettingsRequest(), default), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(await new AppealsController(appeals).StartReview(Guid.NewGuid(), default), Is.TypeOf<NotFoundObjectResult>());
    }

    [TestCase("STUDENT", true, false)]
    [TestCase("STAFF", false, false)]
    [TestCase("ADMIN", false, false)]
    [TestCase("STAFF", true, true)]
    [TestCase("ADMIN", true, true)]
    public async Task StaffActionsRequireRoleAndPermission(string role, bool permission, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy("ManageApplications", policy =>
            policy.RequireAuthenticatedUser().RequireClaim("permission", "ManageApplications")));
        using var provider = services.BuildServiceProvider();
        foreach (var method in new[]
        {
            typeof(CompetitionConclusionsController).GetMethod(nameof(CompetitionConclusionsController.Publish))!,
            typeof(AppealsController).GetMethod(nameof(AppealsController.Resolve))!
        })
        {
            var metadata = method.DeclaringType!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<IAuthorizeData>()
                .Concat(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            var claims = new List<Claim> { new(ClaimTypes.Role, role) };
            if (permission)
                claims.Add(new Claim("permission", "ManageApplications"));
            var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null, policy!);
            Assert.That(result.Succeeded, Is.EqualTo(allowed));
        }
    }

    [Test]
    public void EfModelEnforcesOneAppealDecisionAndEventPerApplication()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        foreach (var (type, property) in new[]
        {
            (typeof(Appeal), "ApplicationId"),
            (typeof(AccommodationEligibility), "ApplicationId"),
            (typeof(OutboxMessage), "EligibilityId")
        })
        {
            Assert.That(db.Model.FindEntityType(type)!.GetIndexes().Any(index => index.IsUnique
                && index.Properties.Select(item => item.Name).SequenceEqual(new[] { property })), Is.True);
        }
    }

    private static void SetProperty(object instance, string property, object value) =>
        instance.GetType().GetProperty(property)!.SetValue(instance, value);

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Store : IConclusionRepository, IStudentClient
    {
        public Competition Competition { get; } = new("2026/27", "Test", null,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
        public Ranking? Preliminary { get; set; }
        public Ranking? Final { get; private set; }
        public List<RankingCandidate> Candidates { get; } = [];
        public List<Appeal> Appeals { get; } = [];
        public List<AccommodationEligibility> Decisions { get; } = [];
        public List<OutboxMessage> Events { get; } = [];
        public Guid StudentId { get; set; }

        public Store()
        {
            Competition.Open();
            Competition.Close();
        }

        public Task<Competition?> GetCompetitionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Competition?>(id == Competition.Id ? Competition : null);

        public Task<Ranking?> GetPreliminaryAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == Competition.Id ? Preliminary : null);

        public Task<Ranking?> GetFinalAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == Competition.Id ? Final : null);

        public Task<IReadOnlyCollection<RankingCandidate>> GetCandidatesAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<RankingCandidate>>(Candidates.ToArray());

        public Task AddAsync(Ranking ranking, CancellationToken ct)
        {
            if (ranking.Type == RankingType.Preliminary)
                Preliminary = ranking;
            else
                Final = ranking;
            return Task.CompletedTask;
        }

        public Task<StudentApplication?> GetApplicationAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Candidates.Select(item => item.Application).SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyCollection<StudentApplication>> GetApplicationsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<StudentApplication>>(Candidates.Select(item => item.Application).ToArray());

        public Task<IReadOnlyCollection<Appeal>> GetAppealsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Appeal>>(Appeals.Where(item => item.CompetitionId == id).ToArray());

        public Task<Appeal?> GetAppealAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Appeals.SingleOrDefault(item => item.Id == id));

        public Task AddAppealAsync(Appeal appeal, CancellationToken ct)
        {
            Appeals.Add(appeal);
            return Task.CompletedTask;
        }

        public Task AddDecisionAsync(AccommodationEligibility decision, CancellationToken ct)
        {
            Decisions.Add(decision);
            return Task.CompletedTask;
        }

        public Task AddEventAsync(OutboxMessage message, CancellationToken ct)
        {
            Events.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AccommodationEligibility>> GetDecisionsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<AccommodationEligibility>>(Decisions.Where(item => item.CompetitionId == id).ToArray());

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);

        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct = default) => Task.FromResult(StudentId);
    }
}
