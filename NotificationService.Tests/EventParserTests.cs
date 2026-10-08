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
            ["ApplicationId"] = Guid.NewGuid(),
            ["CompetitionId"] = Guid.NewGuid(),
            ["WorkerUserId"] = WorkerUserId,
            ["WorkerId"] = Guid.NewGuid(),
            ["ActionId"] = Guid.NewGuid(),
            ["RequestId"] = Guid.NewGuid(),
            ["AccommodationId"] = Guid.NewGuid(),
            ["PurchaseId"] = Guid.NewGuid(),
            ["OccurredAtUtc"] = DateTime.UtcNow
        };
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
