using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NUnit.Framework;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Application.Services;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Infrastructure.Persistence;

namespace StudentCenter.FoodService.Tests;

public sealed class FoodOutboxTests
{
    [Test]
    public void EventContainsPurchaseSnapshotAndStableIdentifier()
    {
        var message = CreateMessage();
        var payload = JsonSerializer.Deserialize<MealPurchasedEvent>(message.Payload)!;
        Assert.Multiple(() =>
        {
            Assert.That(payload.EventId, Is.EqualTo(message.Id));
            Assert.That(payload.PurchaseId, Is.EqualTo(message.PurchaseId));
            Assert.That(message.Type, Is.EqualTo("MealPurchased"));
            Assert.That(payload.MealType, Is.EqualTo("Lunch"));
            Assert.That(payload.Quantity, Is.EqualTo(3));
            Assert.That(payload.UnitPrice, Is.EqualTo(120.25m));
            Assert.That(payload.Amount, Is.EqualTo(360.75m));
            Assert.That(payload.OccurredAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(message.Payload, Does.Not.Contain("RecordedByUserId"));
        });
    }

    [Test]
    public async Task MessagesAreSavedOnlyAfterConfirmationAndEachSuccessIsSavedSeparately()
    {
        var store = new Store(CreateMessage(), CreateMessage());
        var publisher = new Publisher(message =>
        {
            Assert.That(message.PublishedAtUtc, Is.Null);
            Assert.That(store.SavedIds.Contains(message.Id), Is.False);
        });
        await new FoodOutboxDispatcher(store, publisher, TimeProvider.System).DispatchAsync(default);
        Assert.That(store.SavedIds, Is.EquivalentTo(store.Messages.Select(message => message.Id)));
        Assert.That(store.Saves, Is.EqualTo(2));
        Assert.That(publisher.SentIds, Is.EqualTo(store.Messages.Select(message => message.Id)));
    }

    [Test]
    public async Task FailedConfirmationLeavesMessagePendingAndRetryKeepsTheSameEventId()
    {
        var store = new Store(CreateMessage(), CreateMessage());
        var attempts = 0;
        var publisher = new Publisher(_ =>
        {
            if (++attempts == 2)
            {
                throw new IOException("Broker did not confirm.");
            }
        });
        var dispatcher = new FoodOutboxDispatcher(store, publisher, TimeProvider.System);
        Assert.ThrowsAsync<IOException>(() => dispatcher.DispatchAsync(default));
        Assert.That(store.Messages[0].PublishedAtUtc, Is.Not.Null);
        Assert.That(store.Messages[1].PublishedAtUtc, Is.Null);
        Assert.That(store.SavedIds, Is.EquivalentTo(new[] { store.Messages[0].Id }));
        await dispatcher.DispatchAsync(default);
        Assert.That(publisher.SentIds, Is.EqualTo(new[]
        {
            store.Messages[0].Id, store.Messages[1].Id, store.Messages[1].Id
        }));
    }

    [Test]
    public async Task DatabaseFailureAfterConfirmationStopsBatchAndAllowsRedelivery()
    {
        var store = new Store(CreateMessage(), CreateMessage()) { FailSave = true };
        var publisher = new Publisher(_ => { });
        var dispatcher = new FoodOutboxDispatcher(store, publisher, TimeProvider.System);
        Assert.ThrowsAsync<IOException>(() => dispatcher.DispatchAsync(default));
        Assert.That(store.SavedIds, Is.Empty);
        Assert.That(publisher.SentIds, Has.Count.EqualTo(1));
        store.FailSave = false;
        await dispatcher.DispatchAsync(default);
        Assert.That(publisher.SentIds, Is.EqualTo(new[]
        {
            store.Messages[0].Id, store.Messages[0].Id, store.Messages[1].Id
        }));
    }

    [Test]
    public void CancellationBeforeConfirmationDoesNotMarkPublished()
    {
        var store = new Store(CreateMessage());
        var dispatcher = new FoodOutboxDispatcher(store,
            new Publisher(_ => throw new OperationCanceledException()), TimeProvider.System);
        Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(default));
        Assert.That(store.Messages.Single().PublishedAtUtc, Is.Null);
        Assert.That(store.Saves, Is.Zero);
    }

    [Test]
    public void OutboxUsesSameDbContextAndEnforcesOneEventPerPurchase()
    {
        using var db = new FoodDbContext(new DbContextOptionsBuilder<FoodDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var entity = db.Model.FindEntityType(typeof(FoodOutboxMessage))!;
        Assert.That(entity.GetForeignKeys().Single().PrincipalEntityType.ClrType,
            Is.EqualTo(typeof(MealPurchase)));
        Assert.That(entity.GetIndexes().Any(index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "PurchaseId", "Type" })), Is.True);
        Assert.That(entity.FindProperty(nameof(FoodOutboxMessage.Sequence))!.ValueGenerated,
            Is.EqualTo(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd));
    }

    private static FoodOutboxMessage CreateMessage()
    {
        var now = DateTimeOffset.UtcNow;
        var entitlement = new MealEntitlement(Guid.NewGuid(), $"{now.Year}/{now.Year + 1}",
            now.Year, now.Month, StudentCenter.FoodService.Domain.Enums.MealType.Lunch, 1, Guid.NewGuid(), now);
        return FoodOutboxMessage.From(entitlement.Purchase(Guid.NewGuid(), 3, 120.25m, Guid.NewGuid(), now));
    }

    private sealed class Publisher(Action<FoodOutboxMessage> publish) : IFoodEventPublisher
    {
        public List<Guid> SentIds { get; } = [];

        public Task PublishAsync(FoodOutboxMessage message, CancellationToken ct)
        {
            SentIds.Add(message.Id);
            publish(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Store(params FoodOutboxMessage[] messages) : IFoodOutboxRepository
    {
        public FoodOutboxMessage[] Messages { get; } = messages;
        public HashSet<Guid> SavedIds { get; } = [];
        public int Saves { get; private set; }
        public bool FailSave { get; set; }

        public Task<IReadOnlyCollection<FoodOutboxMessage>> GetPendingAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<FoodOutboxMessage>>(
                Messages.Where(message => !SavedIds.Contains(message.Id)).ToArray());

        public Task SaveAsync(CancellationToken ct)
        {
            if (FailSave)
            {
                throw new IOException("Database save failed.");
            }
            Saves++;
            foreach (var message in Messages.Where(message => message.PublishedAtUtc.HasValue))
            {
                SavedIds.Add(message.Id);
            }
            return Task.CompletedTask;
        }
    }
}

