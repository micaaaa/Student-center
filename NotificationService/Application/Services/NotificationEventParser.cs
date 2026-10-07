using System.Globalization;
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
            "notification.application" => type is "ApplicationSubmitted" or "PreliminaryRankingPublished" or "FinalRankingPublished",
            "notification.accommodation" => type is "AccommodationAssigned" or "StudentMovedIn"
                or "StudentMovedOut" or "AccommodationAssignmentCancelled",
            "notification.food" => type == "MealPurchased",
            "notification.maintenance" => type is "MaintenanceWorkerAssigned" or "MaintenanceRequestResolved",
            "notification.billing" => type is "ChargeCreated" or "PaymentRecorded" or "ChargePaid",
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
        string? billingAmount = null;
        if (queue == "notification.application")
        {
            resourceType = type == "ApplicationSubmitted" ? "Application" : "ApplicationResults";
            resourceId = Id(root, "ApplicationId");
            _ = Id(root, "CompetitionId");
        }
        else if (queue == "notification.accommodation")
        {
            resourceType = "Accommodation";
            resourceId = Id(root, "AccommodationId");
        }
        else if (queue == "notification.food")
        {
            resourceType = "MealPurchase";
            resourceId = Id(root, "PurchaseId");
        }
        else if (queue == "notification.billing")
        {
            resourceType = "Charge";
            resourceId = Id(root, "ChargeId");
            if (type is "PaymentRecorded" or "ChargePaid")
            {
                var paymentId = Id(root, "PaymentId");
                if (type == "PaymentRecorded")
                {
                    resourceType = "Payment";
                    resourceId = paymentId;
                }
            }
            else if (root.TryGetProperty("PaymentId", out var payment) && payment.ValueKind != JsonValueKind.Null)
            {
                throw new ArgumentException("A newly created charge cannot reference a payment.");
            }

            var amountField = Property(root, "Amount");
            var currency = Property(root, "Currency");
            if (amountField.ValueKind != JsonValueKind.Number || !amountField.TryGetDecimal(out var amount)
                || amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount
                || currency.ValueKind != JsonValueKind.String || currency.GetString() != "RSD")
            {
                throw new ArgumentException("A positive amount with at most two decimal places in RSD is required.");
            }

            billingAmount = amount.ToString("N2", CultureInfo.GetCultureInfo("sr-Latn-RS")) + " RSD";
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
            "ApplicationSubmitted" => ("Application submitted", "Your accommodation application has been submitted. You can follow its status in My applications."),
            "PreliminaryRankingPublished" => ("Preliminary ranking published", "The preliminary ranking is available. Review your results and the appeal deadline."),
            "FinalRankingPublished" => ("Final ranking published", "The final ranking is available. Open your application results to view the decision."),
            "StudentMovedIn" => ("Evidentirano useljenje", "Vaše useljenje u studentski dom je evidentirano."),
            "StudentMovedOut" => ("Evidentirano iseljenje", "Vaše iseljenje iz studentskog doma je evidentirano."),
            "AccommodationAssignmentCancelled" => ("Dodela smeštaja otkazana", "Vaša dodela smeštaja je otkazana."),
            "MealPurchased" => ("Kupovina obroka", "Kupovina obroka je uspešno evidentirana."),
            "MaintenanceWorkerAssigned" => ("Dodeljen zadatak", "Dodeljen vam je zahtev za održavanje. Proverite trenutnu dodelu pre početka rada."),
            "MaintenanceRequestResolved" => ("Kvar je rešen", "Vaš zahtev za održavanje je rešen. Detalje možete pogledati u istoriji prijave."),
            "ChargeCreated" => ("Novo zaduženje", $"Evidentirano je novo zaduženje u iznosu od {billingAmount}."),
            "PaymentRecorded" => ("Evidentirana uplata", $"Evidentirana je vaša uplata u iznosu od {billingAmount}."),
            "ChargePaid" => ("Zaduženje izmireno", $"Vaše zaduženje u iznosu od {billingAmount} je u potpunosti izmireno."),
            _ => throw new ArgumentException("Unsupported notification event.")
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
