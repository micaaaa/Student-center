using System.Text.Json;

namespace StudentCenter.BillingService.Domain.Entities;

public sealed class BillingOutboxMessage
{
    private BillingOutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public Guid SourceId { get; private set; }
    public Guid ChargeId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public static BillingOutboxMessage ForCharge(Charge charge)
    {
        if (charge.PaidAmount != 0)
        {
            throw new ArgumentException("ChargeCreated must be captured when the unpaid charge is created.");
        }

        return Create("ChargeCreated", charge.Id, charge, null, charge.Amount, charge.CreatedAtUtc);
    }

    public static IReadOnlyCollection<BillingOutboxMessage> ForPayment(Payment payment, Charge charge)
    {
        if (payment.ChargeId != charge.Id || payment.StudentId != charge.StudentId || charge.PaidAmount < payment.Amount)
        {
            throw new ArgumentException("Payment must be recorded against this charge before creating events.");
        }

        var messages = new List<BillingOutboxMessage>
        {
            Create("PaymentRecorded", payment.Id, charge, payment.Id, payment.Amount, payment.PaymentDateUtc)
        };
        if (charge.OutstandingAmount == 0)
        {
            if (charge.PaidAtUtc != payment.PaymentDateUtc)
            {
                throw new ArgumentException("ChargePaid must refer to the payment that settled the charge.");
            }

            messages.Add(Create("ChargePaid", charge.Id, charge, payment.Id, charge.Amount, payment.PaymentDateUtc));
        }

        return messages;
    }

    public void MarkPublished(DateTime now)
    {
        PublishedAtUtc ??= now;
    }

    private static BillingOutboxMessage Create(string type, Guid sourceId, Charge charge,
        Guid? paymentId, decimal amount, DateTime occurredAt)
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc);
        return new BillingOutboxMessage
        {
            Id = id,
            SourceId = sourceId,
            ChargeId = charge.Id,
            PaymentId = paymentId,
            Type = type,
            OccurredAtUtc = timestamp,
            Payload = JsonSerializer.Serialize(new BillingLifecycleEvent(
                id, type, charge.Id, paymentId, charge.StudentId, amount, "RSD", timestamp))
        };
    }
}

// Amount is the payment amount for PaymentRecorded, and the full charge amount for the other events.
// EventId stays unchanged across retries so consumers can deduplicate deliveries.
public sealed record BillingLifecycleEvent(Guid EventId, string Type, Guid ChargeId, Guid? PaymentId,
    Guid StudentId, decimal Amount, string Currency, DateTime OccurredAtUtc);
