using System.ComponentModel.DataAnnotations;

namespace StudentCenter.FoodService.Application.DTOs;

public sealed class MealPurchaseRequest
{
    public Guid RequestId { get; set; }
    public Guid EntitlementId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
