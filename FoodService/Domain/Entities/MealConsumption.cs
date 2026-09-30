using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Domain.Entities;

public sealed class MealConsumption
{
    private MealConsumption()
    {
    }

    internal MealConsumption(Guid requestId, MealEntitlement entitlement, Guid restaurantId,
        Guid actorId, string? cardReference, DateTimeOffset now)
    {
        if (requestId == Guid.Empty || actorId == Guid.Empty || restaurantId == Guid.Empty)
        {
            throw new ArgumentException("Request, staff and restaurant identifiers are required.");
        }

        CardReference = NormalizeCardReference(cardReference);
        Id = Guid.NewGuid();
        RequestId = requestId;
        EntitlementId = entitlement.Id;
        StudentId = entitlement.StudentId;
        MealType = entitlement.MealType;
        RestaurantId = restaurantId;
        RecordedByUserId = actorId;
        ConsumedAtUtc = now.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid EntitlementId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public MealType MealType { get; private set; }
    public DateTime ConsumedAtUtc { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public string? CardReference { get; private set; }

    public static string? NormalizeCardReference(string? reference)
    {
        var cleaned = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (cleaned?.Length > 100)
        {
            throw new ArgumentException("Card reference must not exceed 100 characters.");
        }

        return cleaned;
    }
}
