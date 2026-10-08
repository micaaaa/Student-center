using System.Text.Json;
using NUnit.Framework;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Services;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;

namespace StudentCenter.BillingService.Tests;

[TestFixture]
public sealed class ChargeRulesTests
{
    private static Charge NewCharge(decimal amount, DateOnly? due = null)
    {
        return new Charge(Guid.NewGuid(), ChargeType.Other, amount, due ?? new DateOnly(2026, 10, 10),
            Guid.NewGuid(), "Naknada", "manual:test", Guid.NewGuid(), new string('A', 64), Guid.NewGuid(), DateTimeOffset.UtcNow);
    }

    [Test]
    public void PrepaidMealsCannotBecomeOutstandingCharges()
    {
        Assert.Throws<ArgumentException>(() => new Charge(Guid.NewGuid(), ChargeType.Meal, 100,
            new DateOnly(2026, 10, 10), Guid.NewGuid(), "Obroci", "meal:test", null,
            new string('A', 64), null, DateTimeOffset.UtcNow));
    }

    [Test]
    public void DueDateIsInclusiveAndOverdueIsComputedWithoutBackgroundJob()
    {
        var charge = NewCharge(125.25m);
        Assert.That(charge.Status(new DateOnly(2026, 10, 10)), Is.EqualTo("PENDING"));
        Assert.That(charge.Status(new DateOnly(2026, 10, 11)), Is.EqualTo("OVERDUE"));
        Assert.That(charge.Amount, Is.EqualTo(125.25m));
    }

    [Test]
    public void DelayedMoveInCannotUndoMoveOut()
    {
        var reference = new AccommodationReference(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2026/2027", "StudentMovedOut");
        reference.Apply("StudentMovedIn");
        Assert.That(reference.Stage, Is.EqualTo(3));
    }

    private static MealPurchasedEvent Purchase()
    {
        return new MealPurchasedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2026/2027", 2026, 10, "Lunch", 2, 120.50m, 241m, DateTime.UtcNow);
    }

    private static IncomingBillingEvent Parse(MealPurchasedEvent message)
    {
        return BillingEventParser.Parse("billing.food", "MealPurchased", message.EventId.ToString(),
            JsonSerializer.SerializeToUtf8Bytes(message));
    }

    [Test]
    public void ValidPurchasePreservesExactAmount()
    {
        var purchase = Purchase();
        Assert.That(Parse(purchase).Meal!.Amount, Is.EqualTo(241m));
    }
}
