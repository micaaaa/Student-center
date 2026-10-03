namespace StudentCenter.BillingService.Application.DTOs;

public sealed record MealPurchasedEvent(Guid EventId, Guid PurchaseId, Guid EntitlementId, Guid StudentId,
    string AcademicYear, int Year, int Month, string MealType,
    int Quantity, decimal UnitPrice, decimal Amount, DateTime OccurredAtUtc);

public sealed record AccommodationEvent(Guid EventId, Guid AccommodationId, Guid StudentId, Guid RoomId,
    string AcademicYear, string Type, DateTime OccurredAtUtc);

public sealed record IncomingBillingEvent(Guid EventId, string Fingerprint,
    MealPurchasedEvent? Meal, AccommodationEvent? Accommodation);
