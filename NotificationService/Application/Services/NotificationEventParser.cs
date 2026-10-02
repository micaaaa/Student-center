using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentCenter.NotificationService.Application.DTOs;
using StudentCenter.NotificationService.Domain.Entities;

namespace StudentCenter.NotificationService.Application.Services;

public static class NotificationEventParser
{
    public static bool IsSupported(string queue, string? type)
    {
        return queue switch
        {
            "notification.accommodation" => type is "AccommodationAssigned" or "StudentMovedIn"
                or "StudentMovedOut" or "AccommodationAssignmentCancelled",
            "notification.food" => type == "MealPurchased",
            "notification.maintenance" => type is "MaintenanceWorkerAssigned" or "MaintenanceRequestResolved",
            _ => false
        };
    }

    public static PreparedEvent Parse(string queue, string? type, string? messageId, ReadOnlyMemory<byte> body)
    {
        if (!IsSupported(queue, type) || body.Length is 0 or > 65536)
        {
            throw new ArgumentException("Unsupported notification event or invalid body size.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var eventId = Id(root, "EventId");
        if (!Guid.TryParse(messageId, out var headerId) || headerId != eventId)
        {
            throw new ArgumentException("Event ID does not match message metadata.");
        }

        if (type != "MealPurchased"
            && (!root.TryGetProperty("Type", out var payloadType)
                || payloadType.ValueKind != JsonValueKind.String || payloadType.GetString() != type))
        {
            throw new ArgumentException("Event type does not match message metadata.");
        }

        var timestamp = Property(root, "OccurredAtUtc");
        if (timestamp.ValueKind != JsonValueKind.String || !timestamp.TryGetDateTime(out var occurredAt)
            || occurredAt == default || occurredAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A UTC event timestamp is required.");
        }

        var studentId = Id(root, "StudentId");
        var recipientKind = "STUDENT";
        var recipientId = studentId;
        string resourceType;
        Guid resourceId;
        if (queue == "notification.accommodation")
        {
            resourceType = "Accommodation";
            resourceId = Id(root, "AccommodationId");
        }
        else if (queue == "notification.food")
        {
            resourceType = "MealPurchase";
            resourceId = Id(root, "PurchaseId");
        }
        else
        {
            resourceType = "MaintenanceRequest";
            resourceId = Id(root, "RequestId");
            Id(root, "ActionId");
            Id(root, "WorkerId");
            var workerUserId = Id(root, "WorkerUserId");
            if (type == "MaintenanceWorkerAssigned")
            {
                recipientKind = "USER";
                recipientId = workerUserId;
            }
        }

        var (title, message) = type switch
        {
            "AccommodationAssigned" => ("Dodeljen smeštaj", "Dodeljeno vam je mesto u studentskom domu."),
            "StudentMovedIn" => ("Evidentirano useljenje", "Vaše useljenje u studentski dom je evidentirano."),
            "StudentMovedOut" => ("Evidentirano iseljenje", "Vaše iseljenje iz studentskog doma je evidentirano."),
            "AccommodationAssignmentCancelled" => ("Dodela smeštaja otkazana", "Vaša dodela smeštaja je otkazana."),
            "MealPurchased" => ("Kupovina obroka", "Kupovina obroka je uspešno evidentirana."),
            "MaintenanceWorkerAssigned" => ("Dodeljen zadatak", "Dodeljen vam je zahtev za održavanje. Proverite trenutnu dodelu pre početka rada."),
            _ => ("Kvar je rešen", "Vaš zahtev za održavanje je rešen. Detalje možete pogledati u istoriji prijave.")
        };
        var notification = new Notification(eventId, type!, recipientKind, recipientId,
            title, message, resourceType, resourceId, occurredAt);
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(type + "\n" + Encoding.UTF8.GetString(body.Span))));
        return new PreparedEvent(eventId, fingerprint, new[] { notification });
    }

    private static JsonElement Property(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value))
        {
            throw new ArgumentException($"Missing event field: {name}.");
        }

        return value;
    }

    private static Guid Id(JsonElement root, string name)
    {
        var value = Property(root, name);
        if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var id) || id == Guid.Empty)
        {
            throw new ArgumentException($"Invalid event identifier: {name}.");
        }

        return id;
    }
}
