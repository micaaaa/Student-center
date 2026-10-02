using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;
using StudentCenter.MaintenanceService.Infrastructure.Repositories;

namespace MaintenanceService.Tests;

[TestFixture]
public sealed class OutboxTests
{
    private static (MaintenanceRequest Request, MaintenanceWorker Worker, MaintenanceAction Action) Assignment()
    {
        var worker = new MaintenanceWorker(Guid.NewGuid(), "Serviser", "Vodoinstalater");
        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new MaintenanceCategory("Voda", null), "Kvar", "Opis kvara",
            StudentCenter.MaintenanceService.Domain.Enums.RequestPriority.High, DateTimeOffset.UtcNow);
        request.Accept(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var action = request.Assign(worker, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return (request, worker, action);
    }

    [Test]
    public void AssignmentPayloadContainsRecipientAndStableEventIdentity()
    {
        var (request, worker, action) = Assignment();
        var message = MaintenanceOutboxMessage.From(action, request, worker);
        var payload = JsonSerializer.Deserialize<MaintenanceNotificationEvent>(message.Payload)!;
        Assert.Multiple(() =>
        {
            Assert.That(message.Type, Is.EqualTo("MaintenanceWorkerAssigned"));
            Assert.That(payload.EventId, Is.EqualTo(message.Id));
            Assert.That(payload.ActionId, Is.EqualTo(action.Id));
            Assert.That(payload.WorkerUserId, Is.EqualTo(worker.UserId));
            Assert.That(payload.StudentId, Is.EqualTo(request.StudentId));
            Assert.That(payload.RecordedByUserId, Is.EqualTo(action.RecordedByUserId));
            Assert.That(payload.OccurredAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(message.Payload, Does.Not.Contain(request.Description));
        });
    }

    [Test]
    public void ReassignmentPreservesOldRecipientAndCreatesDistinctEvent()
    {
        var (request, worker, action) = Assignment();
        var original = MaintenanceOutboxMessage.From(action, request, worker);
        var nextWorker = new MaintenanceWorker(Guid.NewGuid(), "Drugi", "Elektricar");
        var nextAction = request.Assign(nextWorker, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var next = MaintenanceOutboxMessage.From(nextAction, request, nextWorker);
        Assert.That(next.Id, Is.Not.EqualTo(original.Id));
        Assert.That(next.ActionId, Is.Not.EqualTo(original.ActionId));
        Assert.That(JsonSerializer.Deserialize<MaintenanceNotificationEvent>(original.Payload)!.WorkerUserId,
            Is.EqualTo(worker.UserId));
    }

    [Test]
    public void ResolutionProducesResolutionEvent()
    {
        var (request, worker, _) = Assignment();
        request.Start(worker.UserId, DateTimeOffset.UtcNow);
        var action = request.Resolve(worker.UserId, "Popravljeno", DateTimeOffset.UtcNow);
        Assert.That(MaintenanceOutboxMessage.From(action, request, worker).Type,
            Is.EqualTo("MaintenanceRequestResolved"));
    }

    [Test]
    public void StartAndUnrelatedWorkerCannotProduceNotification()
    {
        var (request, worker, assignment) = Assignment();
        var start = request.Start(worker.UserId, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => MaintenanceOutboxMessage.From(start, request, worker));
        Assert.Throws<ArgumentException>(() => MaintenanceOutboxMessage.From(assignment, request,
            new MaintenanceWorker(Guid.NewGuid(), "Drugi", "Elektricar")));
    }

    [Test]
    public async Task RepositoryTracksAssignmentAndOutboxTogetherButNotStartEvent()
    {
        var (request, worker, action) = Assignment();
        await using var db = new MaintenanceDbContext(new DbContextOptionsBuilder<MaintenanceDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        db.Requests.Attach(request);
        db.Workers.Attach(worker);
        var repository = new WorkRepository(db);
        await repository.AddActionAsync(action, default);
        await repository.AddActionAsync(request.Start(worker.UserId, DateTimeOffset.UtcNow), default);
        Assert.That(db.ChangeTracker.Entries<MaintenanceAction>().Count(), Is.EqualTo(2));
        var entry = db.ChangeTracker.Entries<MaintenanceOutboxMessage>().Single();
        Assert.That(entry.State, Is.EqualTo(EntityState.Added));
        Assert.That(entry.Entity.ActionId, Is.EqualTo(action.Id));
    }

    [Test]
    public async Task BrokerFailureLeavesPendingMessageForRetryWithSameId()
    {
        var (request, worker, action) = Assignment();
        var message = MaintenanceOutboxMessage.From(action, request, worker);
        var repository = new MemoryOutbox(message);
        var publisher = new Publisher { Fail = true };
        var dispatcher = new MaintenanceOutboxDispatcher(repository, publisher, TimeProvider.System);
        Assert.ThrowsAsync<IOException>(() => dispatcher.DispatchAsync(default));
        Assert.That(message.PublishedAtUtc, Is.Null);
        Assert.That(repository.Saves, Is.Zero);
        publisher.Fail = false;
        await dispatcher.DispatchAsync(default);
        await dispatcher.DispatchAsync(default);
        Assert.That(publisher.Ids, Is.EqualTo(new[] { message.Id, message.Id }));
        Assert.That(repository.Saves, Is.EqualTo(1));
        Assert.That(message.PublishedAtUtc, Is.Not.Null);
    }

    [Test]
    public void PublicationTimestampIsNotOverwritten()
    {
        var (request, worker, action) = Assignment();
        var message = MaintenanceOutboxMessage.From(action, request, worker);
        var now = DateTime.UtcNow;
        message.MarkPublished(now);
        message.MarkPublished(now.AddMinutes(1));
        Assert.That(message.PublishedAtUtc, Is.EqualTo(now));
    }

    private sealed class MemoryOutbox(MaintenanceOutboxMessage message) : IMaintenanceOutboxRepository
    {
        public int Saves { get; private set; }

        public Task<IReadOnlyCollection<MaintenanceOutboxMessage>> GetPendingAsync(CancellationToken ct)
        {
            IReadOnlyCollection<MaintenanceOutboxMessage> pending = message.PublishedAtUtc is null ? [message] : [];
            return Task.FromResult(pending);
        }

        public Task SaveAsync(CancellationToken ct)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class Publisher : IMaintenanceEventPublisher
    {
        public bool Fail { get; set; }
        public List<Guid> Ids { get; } = [];

        public Task PublishAsync(MaintenanceOutboxMessage message, CancellationToken ct)
        {
            Ids.Add(message.Id);
            if (Fail)
            {
                throw new IOException("Broker unavailable");
            }

            return Task.CompletedTask;
        }
    }
}
