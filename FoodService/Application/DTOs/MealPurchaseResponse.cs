using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Application.DTOs;

public sealed record MealPurchaseResponse(
    Guid Id,
    Guid RequestId,
    Guid EntitlementId,
    Guid StudentId,
    string AcademicYear,
    int Year,
    int Month,
    MealType MealType,
    int Quantity,
    decimal UnitPrice,
    decimal Amount,
    DateTime PurchasedAtUtc);

public sealed record PurchaseResult(MealPurchaseResponse Purchase, bool IsReplay);
