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

    private static FoodOutboxMessage CreateMessage()
    {
        var now = DateTimeOffset.UtcNow;
        var entitlement = new MealEntitlement(Guid.NewGuid(), $"{now.Year}/{now.Year + 1}",
            now.Year, now.Month, StudentCenter.FoodService.Domain.Enums.MealType.Lunch, Guid.NewGuid(), now);
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

