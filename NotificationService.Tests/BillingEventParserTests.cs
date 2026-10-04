using System.Text.Json;
using NUnit.Framework;
using StudentCenter.NotificationService.Application.Services;

namespace StudentCenter.NotificationService.Tests;

[TestFixture]
public sealed class BillingEventParserTests
{
    private static Dictionary<string, object?> Payload(string type)
    {
        return new Dictionary<string, object?>
        {
            ["EventId"] = Guid.NewGuid(),
            ["Type"] = type,
            ["StudentId"] = Guid.NewGuid(),
            ["ChargeId"] = Guid.NewGuid(),
            ["PaymentId"] = type == "ChargeCreated" ? null : Guid.NewGuid(),
            ["Amount"] = 1234.50m,
            ["Currency"] = "RSD",
            ["OccurredAtUtc"] = DateTime.UtcNow
        };
    }

    private static Application.DTOs.PreparedEvent Parse(Dictionary<string, object?> payload)
    {
        return NotificationEventParser.Parse("notification.billing", (string)payload["Type"]!,
            payload["EventId"]!.ToString(), JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    [TestCase("ChargeCreated", "Charge", "ChargeId", "Novo zaduženje")]
    [TestCase("PaymentRecorded", "Payment", "PaymentId", "Evidentirana uplata")]
    [TestCase("ChargePaid", "Charge", "ChargeId", "Zaduženje izmireno")]
    public void BillingEventTargetsStudentAndCorrectFinancialResource(string type, string resource, string idField, string title)
    {
        var payload = Payload(type);
        payload["Message"] = "Untrusted text";
        var notification = Parse(payload).Notifications.Single();
        Assert.Multiple(() =>
        {
            Assert.That(notification.RecipientKind, Is.EqualTo("STUDENT"));
            Assert.That(notification.RecipientId, Is.EqualTo(payload["StudentId"]));
            Assert.That(notification.ResourceType, Is.EqualTo(resource));
            Assert.That(notification.ResourceId, Is.EqualTo(payload[idField]));
            Assert.That(notification.Title, Is.EqualTo(title));
            Assert.That(notification.Message, Does.Contain("1.234,50 RSD"));
            Assert.That(notification.Message, Does.Not.Contain("Untrusted"));
            Assert.That(notification.ReadAtUtc, Is.Null);
        });
    }

    [TestCase("ChargeId")]
    [TestCase("StudentId")]
    [TestCase("Amount")]
    [TestCase("Currency")]
    [TestCase("OccurredAtUtc")]
    [TestCase("PaymentId")]
    public void MissingPaymentFieldsAreRejected(string field)
    {
        var payload = Payload("PaymentRecorded");
        payload.Remove(field);
        Assert.Throws<ArgumentException>(() => Parse(payload));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(1.001)]
    public void InvalidAmountsAreRejected(double amount)
    {
        var payload = Payload("ChargeCreated");
        payload["Amount"] = amount;
        Assert.Throws<ArgumentException>(() => Parse(payload));
    }

    [TestCase("Amount", "123.45")]
    [TestCase("Currency", "EUR")]
    [TestCase("ChargeId", "invalid")]
    [TestCase("PaymentId", "00000000-0000-0000-0000-000000000000")]
    [TestCase("OccurredAtUtc", "2026-10-04T12:00:00")]
    public void InvalidFieldsAreRejected(string field, string value)
    {
        var payload = Payload("ChargePaid");
        payload[field] = value;
        Assert.Throws<ArgumentException>(() => Parse(payload));
    }

    [Test]
    public void CreatedChargeCannotReferencePayment()
    {
        var payload = Payload("ChargeCreated");
        payload["PaymentId"] = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => Parse(payload));
    }

    [Test]
    public void WrongQueueOrMetadataIsRejected()
    {
        var payload = Payload("ChargePaid");
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.food",
            "ChargePaid", payload["EventId"]!.ToString(), body));
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.billing",
            "ChargePaid", Guid.NewGuid().ToString(), body));
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.billing",
            "PaymentRecorded", payload["EventId"]!.ToString(), body));
    }

    [Test]
    public void RetryKeepsFingerprintButChangedAmountDoesNot()
    {
        var payload = Payload("PaymentRecorded");
        var original = Parse(payload);
        Assert.That(Parse(payload).Fingerprint, Is.EqualTo(original.Fingerprint));
        payload["Amount"] = 50m;
        Assert.That(Parse(payload).Fingerprint, Is.Not.EqualTo(original.Fingerprint));
    }
}
