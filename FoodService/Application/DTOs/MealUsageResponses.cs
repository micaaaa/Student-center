using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Application.DTOs;

public sealed record MealEntitlementResponse(
    Guid Id,
    Guid StudentId,
    string AcademicYear,
    int Year,
    int Month,
    MealType MealType,
    int AllowedQuantity,
    int ConsumedQuantity,
    int RemainingQuantity,
    MealEntitlementStatus Status);

public sealed record MealConsumptionResponse(
    Guid Id,
    Guid RequestId,
    Guid EntitlementId,
    Guid StudentId,
    Guid RestaurantId,
    MealType MealType,
    DateTime ConsumedAtUtc);

public sealed record ConsumptionResult(MealConsumptionResponse Consumption, bool IsReplay);
