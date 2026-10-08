using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Application.Services;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.Tests;

public sealed class AccommodationOutboxTests
{

    [Test]
    public async Task FailedConfirmationLeavesMessagePendingAndRetryKeepsTheSameEventId()
    {
        var store = new Store(CreateMessage(), CreateMessage());
        var attempts = 0;
        var publisher = new Publisher(_ =>
        {
            if (++attempts == 2)
                throw new IOException("Broker did not confirm.");
        });
        var dispatcher = new AccommodationOutboxDispatcher(store, publisher, TimeProvider.System);
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

    private static AccommodationOutboxMessage CreateMessage()
    {
        var now = DateTime.UtcNow;
        var eligibility = new ReceivedEligibility(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "2026/2027", now);
        return AccommodationOutboxMessage.From(new StudentAccommodation(
            eligibility, Guid.NewGuid(), Guid.NewGuid(), now));
    }

    private sealed class Publisher(Action<AccommodationOutboxMessage> publish) : IAccommodationEventPublisher
    {
        public List<Guid> SentIds { get; } = [];

        public Task PublishAsync(AccommodationOutboxMessage message, CancellationToken ct)
        {
            SentIds.Add(message.Id);
            publish(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Store(params AccommodationOutboxMessage[] messages) : IAccommodationOutboxRepository
    {
        public AccommodationOutboxMessage[] Messages { get; } = messages;
        public HashSet<Guid> SavedIds { get; } = [];
        public int Saves { get; private set; }
        public bool FailSave { get; set; }

        public Task<IReadOnlyCollection<AccommodationOutboxMessage>> GetPendingAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<AccommodationOutboxMessage>>(
                Messages.Where(message => !SavedIds.Contains(message.Id)).ToArray());

        public Task SaveAsync(CancellationToken ct)
        {
            if (FailSave)
                throw new IOException("Database save failed.");
            Saves++;
            foreach (var message in Messages.Where(message => message.PublishedAtUtc.HasValue))
                SavedIds.Add(message.Id);
            return Task.CompletedTask;
        }
    }
}
