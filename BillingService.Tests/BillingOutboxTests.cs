using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Application.Services;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;
using StudentCenter.BillingService.Infrastructure.Persistence;
using StudentCenter.BillingService.Infrastructure.Repositories;

namespace StudentCenter.BillingService.Tests;

[TestFixture]
public sealed class BillingOutboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static Charge NewCharge()
    {
        return new Charge(Guid.NewGuid(), ChargeType.Other, 100, new DateOnly(2026, 10, 10),
            Guid.NewGuid(), "Private charge notes", "manual:" + Guid.NewGuid(), Guid.NewGuid(),
            new string('A', 64), Guid.NewGuid(), Now);
    }

    [Test]
    public void FinalPaymentEmitsPaymentAndSettlementWithDifferentAmountsAndIdentities()
    {
        var charge = NewCharge();
        charge.RecordPayment(Guid.NewGuid(), 30, PaymentMethod.Cash, null, Guid.NewGuid(), Now);
        var payment = charge.RecordPayment(Guid.NewGuid(), 70, PaymentMethod.Cash, null, Guid.NewGuid(), Now.AddMinutes(1));
        var messages = BillingOutboxMessage.ForPayment(payment, charge).ToArray();
        var recorded = messages.Single(message => message.Type == "PaymentRecorded");
        var paid = messages.Single(message => message.Type == "ChargePaid");
        Assert.That(recorded.Id, Is.Not.EqualTo(paid.Id));
        Assert.That(recorded.SourceId, Is.EqualTo(payment.Id));
        Assert.That(paid.SourceId, Is.EqualTo(charge.Id));
        Assert.That(JsonSerializer.Deserialize<BillingLifecycleEvent>(recorded.Payload)!.Amount, Is.EqualTo(70));
        Assert.That(JsonSerializer.Deserialize<BillingLifecycleEvent>(paid.Payload)!.Amount, Is.EqualTo(100));
        Assert.That(paid.PaymentId, Is.EqualTo(payment.Id));
    }

    [Test]
    public async Task FailedPublishRemainsPendingAndRetryUsesSameEventId()
    {
        var message = BillingOutboxMessage.ForCharge(NewCharge());
        var repository = new MemoryOutbox(message);
        var publisher = new Publisher { Fail = true };
        var dispatcher = new BillingOutboxDispatcher(repository, publisher, TimeProvider.System);
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

    private sealed class MemoryOutbox(BillingOutboxMessage message) : IBillingOutboxRepository
    {
        public int Saves { get; private set; }

        public Task<IReadOnlyCollection<BillingOutboxMessage>> GetPendingAsync(CancellationToken ct)
        {
            IReadOnlyCollection<BillingOutboxMessage> pending = message.PublishedAtUtc is null ? [message] : [];
            return Task.FromResult(pending);
        }

        public Task SaveAsync(CancellationToken ct)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class Publisher : IBillingEventPublisher
    {
        public bool Fail { get; set; }
        public List<Guid> Ids { get; } = [];

        public Task PublishAsync(BillingOutboxMessage message, CancellationToken ct)
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
