using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Infrastructure.Persistence;

namespace StudentCenter.ApplicationService.Tests;

[TestFixture]
public sealed class NotificationOutboxTests
{
    private sealed class SuppressDatabaseSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=unused;Database=unused;Integrated Security=True")
        .AddInterceptors(new SuppressDatabaseSave()).Options);

    [Test]
    public async Task SubmissionQueuesOnceAndDraftDoesNotQueue()
    {
        await using var db = Context();
        var application = new StudentApplication(Guid.NewGuid(), Guid.NewGuid());
        db.Attach(application);
        await db.SaveChangesAsync();
        Assert.That(db.OutboxMessages.Local, Is.Empty);
        application.Submit();
        await db.SaveChangesAsync();
        await db.SaveChangesAsync();
        Assert.That(db.OutboxMessages.Local.Single().Type, Is.EqualTo("ApplicationSubmitted"));
        Assert.That(db.OutboxMessages.Local.Single().EligibilityId, Is.Null);
    }

    [TestCase(RankingType.Preliminary, "PreliminaryRankingPublished")]
    [TestCase(RankingType.Final, "FinalRankingPublished")]
    public async Task PublicationQueuesForEveryRankedStudent(RankingType type, string eventType)
    {
        await using var db = Context();
        var ranking = new Ranking(Guid.NewGuid(), type);
        ranking.ReplaceDraft(new[] {
            new RankingEntry(ranking.Id, Guid.NewGuid(), Guid.NewGuid(), 1, 80),
            new RankingEntry(ranking.Id, Guid.NewGuid(), Guid.NewGuid(), 2, 70)
        }, "fingerprint", RankingTieRule.EarlierSubmission, Guid.NewGuid());
        db.Attach(ranking);
        await db.SaveChangesAsync();
        Assert.That(db.OutboxMessages.Local, Is.Empty);
        ranking.Publish(Guid.NewGuid());
        await db.SaveChangesAsync();
        Assert.That(db.OutboxMessages.Local, Has.Count.EqualTo(2));
        Assert.That(db.OutboxMessages.Local.All(item => item.Type == eventType), Is.True);
        db.ChangeTracker.AcceptAllChanges();
        await db.SaveChangesAsync();
        Assert.That(db.OutboxMessages.Local, Has.Count.EqualTo(2));
    }
}
