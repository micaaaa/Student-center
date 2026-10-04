using System.ComponentModel.DataAnnotations;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;

namespace StudentCenter.BillingService.Application.DTOs;

public sealed class RecordPaymentRequest
{
    public Guid RequestId { get; set; }
    public Guid ChargeId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }
}

public sealed record PaymentResponse(Guid Id, Guid ChargeId, Guid StudentId, decimal Amount,
    string Currency, PaymentMethod Method, string? ReferenceNumber, DateTime PaymentDateUtc)
{
    public static PaymentResponse From(Payment payment)
    {
        return new PaymentResponse(payment.Id, payment.ChargeId, payment.StudentId, payment.Amount,
            "RSD", payment.Method, payment.ReferenceNumber, DateTime.SpecifyKind(payment.PaymentDateUtc, DateTimeKind.Utc));
    }
}

public sealed record PaymentResult(PaymentResponse Payment, bool AlreadyExists);
