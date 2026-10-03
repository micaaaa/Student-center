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

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("1.001")]
    [TestCase("10000000000000000")]
    public void InvalidMoneyIsRejected(string amount)
    {
        Assert.Throws<ArgumentException>(() => NewCharge(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
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
    public void MissingDueDateIsRejected()
    {
        Assert.Throws<ArgumentException>(() => NewCharge(100, default(DateOnly)));
    }

    [Test]
    public void DelayedAccommodationAssignmentCannotUndoCancellation()
    {
        var reference = new AccommodationReference(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2026/2027", "AccommodationAssignmentCancelled");
        reference.Apply("AccommodationAssigned");
        reference.Apply("StudentMovedIn");
        Assert.That(reference.Stage, Is.EqualTo(4));
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

    [TestCase("amount")]
    [TestCase("price")]
    [TestCase("quantity")]
    [TestCase("student")]
    [TestCase("purchase")]
    [TestCase("period")]
    [TestCase("academicYear")]
    [TestCase("mealType")]
    [TestCase("timestamp")]
    public void InvalidPurchaseCannotCreateDebt(string field)
    {
        var message = Purchase();
        message = field switch
        {
            "amount" => message with { Amount = 240m },
            "price" => message with { UnitPrice = 0 },
            "quantity" => message with { Quantity = 0 },
            "student" => message with { StudentId = Guid.Empty },
            "purchase" => message with { PurchaseId = Guid.Empty },
            "period" => message with { Month = 13 },
            "academicYear" => message with { AcademicYear = "2026/2028" },
            "mealType" => message with { MealType = "Other" },
            _ => message with { OccurredAtUtc = DateTime.SpecifyKind(message.OccurredAtUtc, DateTimeKind.Unspecified) }
        };
        Assert.Throws<ArgumentException>(() => Parse(message));
    }

    [Test]
    public void WrongMetadataAndQueueAreRejected()
    {
        var message = Purchase();
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        Assert.Throws<ArgumentException>(() => BillingEventParser.Parse("billing.food", "MealPurchased", Guid.NewGuid().ToString(), body));
        Assert.Throws<ArgumentException>(() => BillingEventParser.Parse("billing.accommodation", "MealPurchased", message.EventId.ToString(), body));
        Assert.Throws<ArgumentException>(() => BillingEventParser.Parse("billing.food", "Unknown", message.EventId.ToString(), body));
    }

    [TestCase("AccommodationAssigned")]
    [TestCase("StudentMovedIn")]
    [TestCase("StudentMovedOut")]
    [TestCase("AccommodationAssignmentCancelled")]
    public void AccommodationEventsOnlyCarryReferencesWithoutInventingPrice(string type)
    {
        var message = new AccommodationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2026/2027", type, DateTime.UtcNow);
        var parsed = BillingEventParser.Parse("billing.accommodation", type, message.EventId.ToString(),
            JsonSerializer.SerializeToUtf8Bytes(message));
        Assert.That(parsed.Meal, Is.Null);
        Assert.That(parsed.Accommodation, Is.EqualTo(message));
    }

    [Test]
    public void MalformedBodyIsRejected()
    {
        Assert.Catch<JsonException>(() => BillingEventParser.Parse("billing.food", "MealPurchased", "id", "{"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => BillingEventParser.Parse("billing.food", "MealPurchased", "id", new byte[65537]));
    }
}
