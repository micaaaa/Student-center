using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Domain.Entities;

public sealed class MealPurchase
{
    private MealPurchase()
    {
    }

    internal MealPurchase(Guid requestId, MealEntitlement entitlement, int quantity,
        decimal unitPrice, Guid actorId, DateTimeOffset now)
    {
        Validate(requestId, entitlement.Id, quantity, unitPrice, actorId);
        Id = Guid.NewGuid();
        RequestId = requestId;
        EntitlementId = entitlement.Id;
        StudentId = entitlement.StudentId;
        AcademicYear = entitlement.AcademicYear;
        Year = entitlement.Year;
        Month = entitlement.Month;
        MealType = entitlement.MealType;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Amount = quantity * unitPrice;
        RecordedByUserId = actorId;
        PurchasedAtUtc = now.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid EntitlementId { get; private set; }
    public Guid StudentId { get; private set; }
    public string AcademicYear { get; private set; } = null!;
    public int Year { get; private set; }
    public int Month { get; private set; }
    public MealType MealType { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PurchasedAtUtc { get; private set; }
    public Guid RecordedByUserId { get; private set; }

    public static void Validate(Guid requestId, Guid entitlementId, int quantity, decimal unitPrice, Guid actorId)
    {
        if (requestId == Guid.Empty || entitlementId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Request, entitlement and staff identifiers are required.");
        }

        if (quantity < 1)
        {
            throw new ArgumentException("Purchase quantity must be positive.");
        }

        if (unitPrice <= 0 || unitPrice > 99999999.99m || decimal.Round(unitPrice, 2) != unitPrice)
        {
            throw new ArgumentException("Unit price must be positive, at most 99999999.99, with at most two decimal places.");
        }

        if (quantity * unitPrice > 9999999999999999.99m)
        {
            throw new ArgumentException("Purchase amount exceeds the supported limit.");
        }
    }
}
