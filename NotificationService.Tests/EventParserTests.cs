using System.Text.Json;
using NUnit.Framework;
using StudentCenter.NotificationService.Application.Services;

namespace StudentCenter.NotificationService.Tests;

[TestFixture]
public sealed class EventParserTests
{
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid WorkerUserId = Guid.NewGuid();

    private static Dictionary<string, object> Payload(string type)
    {
        return new Dictionary<string, object>
        {
            ["EventId"] = Guid.NewGuid(),
            ["Type"] = type,
            ["StudentId"] = StudentId,
            ["WorkerUserId"] = WorkerUserId,
            ["WorkerId"] = Guid.NewGuid(),
            ["ActionId"] = Guid.NewGuid(),
            ["RequestId"] = Guid.NewGuid(),
            ["AccommodationId"] = Guid.NewGuid(),
            ["PurchaseId"] = Guid.NewGuid(),
            ["OccurredAtUtc"] = DateTime.UtcNow
        };
    }

    [TestCase("notification.accommodation", "AccommodationAssigned", "Accommodation")]
    [TestCase("notification.accommodation", "StudentMovedIn", "Accommodation")]
    [TestCase("notification.accommodation", "StudentMovedOut", "Accommodation")]
    [TestCase("notification.accommodation", "AccommodationAssignmentCancelled", "Accommodation")]
    [TestCase("notification.food", "MealPurchased", "MealPurchase")]
    [TestCase("notification.maintenance", "MaintenanceRequestResolved", "MaintenanceRequest")]
    public void StudentEventsTargetProfileRatherThanAccount(string queue, string type, string resource)
    {
        var payload = Payload(type);
        if (type == "MealPurchased")
        {
            payload.Remove("Type");
        }

        var result = NotificationEventParser.Parse(queue, type, payload["EventId"].ToString(),
            JsonSerializer.SerializeToUtf8Bytes(payload));
        var notification = result.Notifications.Single();
        Assert.Multiple(() =>
        {
            Assert.That(notification.RecipientKind, Is.EqualTo("STUDENT"));
            Assert.That(notification.RecipientId, Is.EqualTo(StudentId));
            Assert.That(notification.ResourceType, Is.EqualTo(resource));
            Assert.That(notification.ReadAtUtc, Is.Null);
        });
    }

    [Test]
    public void AssignmentTargetsWorkerAccountAndIgnoresUntrustedText()
    {
        var payload = Payload("MaintenanceWorkerAssigned");
        payload["Message"] = "Untrusted message";
        var result = NotificationEventParser.Parse("notification.maintenance", "MaintenanceWorkerAssigned",
            payload["EventId"].ToString(), JsonSerializer.SerializeToUtf8Bytes(payload));
        var notification = result.Notifications.Single();
        Assert.That(notification.RecipientId, Is.EqualTo(WorkerUserId));
        Assert.That(notification.RecipientKind, Is.EqualTo("USER"));
        Assert.That(notification.Message, Does.Not.Contain("Untrusted"));
    }

    [TestCase("EventId")]
    [TestCase("StudentId")]
    [TestCase("RequestId")]
    [TestCase("WorkerUserId")]
    [TestCase("WorkerId")]
    [TestCase("ActionId")]
    [TestCase("OccurredAtUtc")]
    [TestCase("Type")]
    public void MissingRequiredFieldIsRejected(string field)
    {
        var payload = Payload("MaintenanceWorkerAssigned");
        var id = payload["EventId"].ToString();
        payload.Remove(field);
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.maintenance",
            "MaintenanceWorkerAssigned", id, JsonSerializer.SerializeToUtf8Bytes(payload)));
    }

    [TestCase("EventId", "wrong")]
    [TestCase("StudentId", "00000000-0000-0000-0000-000000000000")]
    [TestCase("OccurredAtUtc", "2026-10-02T10:00:00")]
    [TestCase("Type", "MaintenanceRequestResolved")]
    public void InvalidFieldsAreRejected(string field, string value)
    {
        var payload = Payload("MaintenanceWorkerAssigned");
        var id = payload["EventId"].ToString();
        payload[field] = value;
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.maintenance",
            "MaintenanceWorkerAssigned", id, JsonSerializer.SerializeToUtf8Bytes(payload)));
    }

    [Test]
    public void WrongQueueAndMetadataAreRejected()
    {
        var payload = Payload("MealPurchased");
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.food",
            "MealPurchased", Guid.NewGuid().ToString(), body));
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.maintenance",
            "MealPurchased", payload["EventId"].ToString(), body));
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.food",
            "Unknown", payload["EventId"].ToString(), body));
    }

    [Test]
    public void MalformedOversizedAndNonObjectBodiesAreRejected()
    {
        Assert.Catch<JsonException>(() => NotificationEventParser.Parse("notification.food", "MealPurchased", "id", "{"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.food", "MealPurchased", "id", "[]"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => NotificationEventParser.Parse("notification.food", "MealPurchased", "id", new byte[65537]));
    }

    [Test]
    public void RedeliveryKeepsFingerprintAndChangedContentDoesNot()
    {
        var payload = Payload("MealPurchased");
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        var original = NotificationEventParser.Parse("notification.food", "MealPurchased", payload["EventId"].ToString(), body);
        var repeated = NotificationEventParser.Parse("notification.food", "MealPurchased", payload["EventId"].ToString(), body);
        payload["StudentId"] = Guid.NewGuid();
        var modified = NotificationEventParser.Parse("notification.food", "MealPurchased", payload["EventId"].ToString(),
            JsonSerializer.SerializeToUtf8Bytes(payload));
        Assert.That(repeated.Fingerprint, Is.EqualTo(original.Fingerprint));
        Assert.That(modified.Fingerprint, Is.Not.EqualTo(original.Fingerprint));
    }
}

