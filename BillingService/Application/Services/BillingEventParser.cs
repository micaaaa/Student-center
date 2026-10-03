using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Domain.Entities;

namespace StudentCenter.BillingService.Application.Services;

public static class BillingEventParser
{
    public static IncomingBillingEvent Parse(string queue, string? type, string? messageId, ReadOnlyMemory<byte> body)
    {
        if (body.Length is 0 or > 65536)
        {
            throw new ArgumentException("Invalid event body size.");
        }

        var fingerprint = Hash(type + "\n" + Encoding.UTF8.GetString(body.Span));
        if (queue == "billing.food" && type == "MealPurchased")
        {
            var meal = JsonSerializer.Deserialize<MealPurchasedEvent>(body.Span)
                ?? throw new ArgumentException("Meal purchase data is required.");
            ValidateMetadata(meal.EventId, meal.OccurredAtUtc, messageId);
            ValidateAcademicYear(meal.AcademicYear);
            Charge.ValidateAmount(meal.Amount);
            if (meal.PurchaseId == Guid.Empty || meal.EntitlementId == Guid.Empty || meal.StudentId == Guid.Empty
                || meal.Year is < 1 or > 9998 || meal.Month is < 1 or > 12
                || meal.MealType is not ("Breakfast" or "Lunch" or "Dinner")
                || meal.Quantity < 1 || meal.UnitPrice <= 0 || meal.UnitPrice > 99999999.99m
                || decimal.Round(meal.UnitPrice, 2) != meal.UnitPrice || meal.Amount != meal.Quantity * meal.UnitPrice)
            {
                throw new ArgumentException("Invalid meal purchase references, period or amount.");
            }

            return new IncomingBillingEvent(meal.EventId, fingerprint, meal, null);
        }

        if (queue == "billing.accommodation" && type is "AccommodationAssigned" or "StudentMovedIn"
            or "StudentMovedOut" or "AccommodationAssignmentCancelled")
        {
            var accommodation = JsonSerializer.Deserialize<AccommodationEvent>(body.Span)
                ?? throw new ArgumentException("Accommodation data is required.");
            ValidateMetadata(accommodation.EventId, accommodation.OccurredAtUtc, messageId);
            ValidateAcademicYear(accommodation.AcademicYear);
            if (accommodation.AccommodationId == Guid.Empty || accommodation.StudentId == Guid.Empty
                || accommodation.RoomId == Guid.Empty || accommodation.Type != type)
            {
                throw new ArgumentException("Invalid accommodation event references or type.");
            }

            return new IncomingBillingEvent(accommodation.EventId, fingerprint, null, accommodation);
        }

        throw new ArgumentException("Unsupported billing event.");
    }

    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static void ValidateMetadata(Guid id, DateTime timestamp, string? messageId)
    {
        if (id == Guid.Empty || !Guid.TryParse(messageId, out var headerId) || headerId != id
            || timestamp == default || timestamp.Kind != DateTimeKind.Utc || timestamp.Year > 9998)
        {
            throw new ArgumentException("Invalid event identity or UTC timestamp.");
        }
    }

    private static void ValidateAcademicYear(string value)
    {
        if (value is null || value.Length != 9 || value[4] != '/'
            || !int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(value.AsSpan(5, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || start < 1 || end != start + 1)
        {
            throw new ArgumentException("Academic year must use YYYY/YYYY with consecutive years.");
        }
    }
}
