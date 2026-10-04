using StudentCenter.BillingService.Domain.Enums;

namespace StudentCenter.BillingService.Domain.Entities;

public sealed class Payment
{
    private Payment()
    {
    }

    internal Payment(Guid requestId, Charge charge, decimal amount, PaymentMethod method,
        string? referenceNumber, Guid actorId, DateTimeOffset now)
    {
        Validate(requestId, charge.Id, amount, method, referenceNumber, actorId);
        Id = Guid.NewGuid();
        RequestId = requestId;
        ChargeId = charge.Id;
        StudentId = charge.StudentId;
        Amount = amount;
        Method = method;
        ReferenceNumber = NormalizeReference(referenceNumber);
        RecordedByUserId = actorId;
        PaymentDateUtc = now.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ChargeId { get; private set; }
    public Guid StudentId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTime PaymentDateUtc { get; private set; }

    public static void Validate(Guid requestId, Guid chargeId, decimal amount, PaymentMethod method,
        string? referenceNumber, Guid actorId)
    {
        if (requestId == Guid.Empty || chargeId == Guid.Empty || actorId == Guid.Empty || !Enum.IsDefined(method))
        {
            throw new ArgumentException("Valid request, charge, actor and payment method are required.");
        }

        Charge.ValidateAmount(amount);
        var reference = NormalizeReference(referenceNumber);
        if (reference?.Length > 100 || method != PaymentMethod.Cash && reference is null)
        {
            throw new ArgumentException("Card and bank payments require a unique transaction reference of at most 100 characters.");
        }
    }

    public static string? NormalizeReference(string? referenceNumber)
    {
        return string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim().ToUpperInvariant();
    }
}
