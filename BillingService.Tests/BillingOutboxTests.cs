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
    public void ChargeEventContainsStudentIdentityAndImmutableFinancialSnapshot()
    {
        var charge = NewCharge();
        var message = BillingOutboxMessage.ForCharge(charge);
        var payload = JsonSerializer.Deserialize<BillingLifecycleEvent>(message.Payload)!;
        Assert.Multiple(() =>
        {
            Assert.That(message.Type, Is.EqualTo("ChargeCreated"));
            Assert.That(message.SourceId, Is.EqualTo(charge.Id));
            Assert.That(payload.EventId, Is.EqualTo(message.Id));
            Assert.That(payload.StudentId, Is.EqualTo(charge.StudentId));
            Assert.That(payload.Amount, Is.EqualTo(100));
            Assert.That(payload.Currency, Is.EqualTo("RSD"));
            Assert.That(payload.PaymentId, Is.Null);
            Assert.That(payload.OccurredAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(message.Payload, Does.Not.Contain("Private charge notes"));
        });
        charge.RecordPayment(Guid.NewGuid(), 50, PaymentMethod.Cash, null, Guid.NewGuid(), Now);
        Assert.That(JsonSerializer.Deserialize<BillingLifecycleEvent>(message.Payload), Is.EqualTo(payload));
    }

    [Test]
    public void PartialPaymentDoesNotEmitChargePaid()
    {
        var charge = NewCharge();
        var payment = charge.RecordPayment(Guid.NewGuid(), 30, PaymentMethod.Card, "private-ref", Guid.NewGuid(), Now);
        var message = BillingOutboxMessage.ForPayment(payment, charge).Single();
        var payload = JsonSerializer.Deserialize<BillingLifecycleEvent>(message.Payload)!;
        Assert.That(message.Type, Is.EqualTo("PaymentRecorded"));
        Assert.That(message.SourceId, Is.EqualTo(payment.Id));
        Assert.That(payload.PaymentId, Is.EqualTo(payment.Id));
        Assert.That(payload.Amount, Is.EqualTo(30));
        Assert.That(message.Payload, Does.Not.Contain("PRIVATE-REF"));
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
    public void AnotherChargeCannotBeUsedForPaymentEvents()
    {
        var charge = NewCharge();
        var payment = charge.RecordPayment(Guid.NewGuid(), 10, PaymentMethod.Cash, null, Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => BillingOutboxMessage.ForPayment(payment, NewCharge()));
        Assert.Throws<ArgumentException>(() => BillingOutboxMessage.ForCharge(charge));
    }

    [Test]
    public void EarlierPaymentCannotBeReportedAsSettlement()
    {
        var charge = NewCharge();
        var earlier = charge.RecordPayment(Guid.NewGuid(), 10, PaymentMethod.Cash, null, Guid.NewGuid(), Now);
        charge.RecordPayment(Guid.NewGuid(), 90, PaymentMethod.Cash, null, Guid.NewGuid(), Now.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => BillingOutboxMessage.ForPayment(earlier, charge));
    }

    [Test]
    public async Task PaymentRepositoryTracksPaymentAndBothFinalEventsTogether()
    {
        var charge = NewCharge();
        await using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        db.Charges.Attach(charge);
        var payment = charge.RecordPayment(Guid.NewGuid(), 100, PaymentMethod.Cash, null, Guid.NewGuid(), Now);
        await new PaymentRepository(db).AddAsync(payment, default);
        Assert.That(db.ChangeTracker.Entries<Payment>().Single().State, Is.EqualTo(EntityState.Added));
        Assert.That(db.ChangeTracker.Entries<BillingOutboxMessage>().Count(), Is.EqualTo(2));
        Assert.That(db.ChangeTracker.Entries<BillingOutboxMessage>().All(entry => entry.State == EntityState.Added), Is.True);
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

    [Test]
    public void FirstPublicationTimestampIsPreserved()
    {
        var message = BillingOutboxMessage.ForCharge(NewCharge());
        message.MarkPublished(Now.UtcDateTime);
        message.MarkPublished(Now.AddMinutes(1).UtcDateTime);
        Assert.That(message.PublishedAtUtc, Is.EqualTo(Now.UtcDateTime));
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
