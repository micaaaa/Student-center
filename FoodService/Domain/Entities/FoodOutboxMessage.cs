using System.Text.Json;

namespace StudentCenter.FoodService.Domain.Entities;

public sealed class FoodOutboxMessage
{
    private FoodOutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public Guid PurchaseId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public static FoodOutboxMessage From(MealPurchase purchase)
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.SpecifyKind(purchase.PurchasedAtUtc, DateTimeKind.Utc);
        return new FoodOutboxMessage
        {
            Id = id,
            PurchaseId = purchase.Id,
            Type = "MealPurchased",
            OccurredAtUtc = timestamp,
            Payload = JsonSerializer.Serialize(new MealPurchasedEvent(
                id, purchase.Id, purchase.EntitlementId, purchase.StudentId,
                purchase.AcademicYear, purchase.Year, purchase.Month, purchase.MealType.ToString(),
                purchase.Quantity, purchase.UnitPrice, purchase.Amount, timestamp))
        };
    }

    public void MarkPublished(DateTime now)
    {
        PublishedAtUtc ??= now;
    }
}

public sealed record MealPurchasedEvent(
    Guid EventId, Guid PurchaseId, Guid EntitlementId, Guid StudentId,
    string AcademicYear, int Year, int Month, string MealType,
    int Quantity, decimal UnitPrice, decimal Amount, DateTime OccurredAtUtc);
