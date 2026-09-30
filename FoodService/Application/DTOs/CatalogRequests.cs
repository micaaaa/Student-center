using System.ComponentModel.DataAnnotations;
using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Application.DTOs;

public sealed class RestaurantRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string Address { get; set; } = string.Empty;

    [EnumDataType(typeof(RestaurantStatus))]
    public RestaurantStatus Status { get; set; } = RestaurantStatus.Active;
}

public sealed class MenuRequest
{
    public DateOnly Date { get; set; }

    [Required, MinLength(1), MaxLength(30)]
    public List<MealRequest> Meals { get; set; } = [];
}

public sealed class MealRequest
{
    [EnumDataType(typeof(MealType))]
    public MealType Type { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public decimal Price { get; set; }
}
