using System.ComponentModel.DataAnnotations;
using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Application.DTOs;

public sealed class CreateMealEntitlementRequest
{
    public Guid StudentId { get; set; }

    [Required, MaxLength(9)]
    public string AcademicYear { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    [EnumDataType(typeof(MealType))]
    public MealType MealType { get; set; }
}

public sealed class UpdateMealEntitlementRequest
{
    [EnumDataType(typeof(MealEntitlementStatus))]
    public MealEntitlementStatus Status { get; set; }
}

public sealed class ConsumeMealRequest
{
    public Guid RequestId { get; set; }
    public Guid EntitlementId { get; set; }
    public Guid RestaurantId { get; set; }

    [MaxLength(100)]
    public string? CardReference { get; set; }
}
