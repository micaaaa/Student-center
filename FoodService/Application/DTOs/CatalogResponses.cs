using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Application.DTOs;

public sealed record RestaurantResponse(
    Guid Id,
    string Name,
    string Address,
    RestaurantStatus Status);

public sealed record MenuResponse(
    Guid Id,
    Guid RestaurantId,
    DateOnly Date,
    MenuStatus Status,
    IReadOnlyCollection<MealResponse> Meals);

public sealed record MealResponse(
    Guid Id,
    MealType Type,
    string Name,
    string? Description,
    decimal Price);
