using System.ComponentModel.DataAnnotations;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;

namespace StudentCenter.BillingService.Application.DTOs;

public sealed class CreateChargeRequest
{
    public Guid RequestId { get; set; }
    public Guid StudentId { get; set; }
    public ChargeType Type { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid ReferenceId { get; set; }

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(7)]
    public string? Period { get; set; }
}

public sealed record ChargeResponse(Guid Id, Guid StudentId, ChargeType Type, decimal Amount,
    string Currency, DateOnly DueDate, string Status, Guid ReferenceId, string? Period,
    string Description, DateTime CreatedAtUtc, decimal PaidAmount, decimal OutstandingAmount, DateTime? PaidAtUtc)
{
    public static ChargeResponse From(Charge charge, DateOnly today)
    {
        return new ChargeResponse(charge.Id, charge.StudentId, charge.Type, charge.Amount, "RSD",
            charge.DueDate, charge.Status(today), charge.ReferenceId, charge.Period, charge.Description,
            DateTime.SpecifyKind(charge.CreatedAtUtc, DateTimeKind.Utc), charge.PaidAmount, charge.OutstandingAmount,
            charge.PaidAtUtc.HasValue ? DateTime.SpecifyKind(charge.PaidAtUtc.Value, DateTimeKind.Utc) : null);
    }
}

public sealed record ChargeResult(ChargeResponse Charge, bool AlreadyExists);
public sealed record BalanceResponse(string Currency, int ChargeCount, decimal OutstandingAmount,
    decimal OverdueAmount, decimal TotalChargedAmount, decimal TotalPaidAmount);
