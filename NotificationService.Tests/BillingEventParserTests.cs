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

    [TestCase("ChargeCreated", "Charge", "ChargeId", "New charge")]
    [TestCase("PaymentRecorded", "Payment", "PaymentId", "Payment recorded")]
    [TestCase("ChargePaid", "Charge", "ChargeId", "Charge paid")]
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
}
