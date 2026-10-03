using StudentCenter.BillingService.Domain.Enums;

namespace StudentCenter.BillingService.Domain.Entities;

public sealed class Charge
{
    private Charge()
    {
    }

    public Charge(Guid studentId, ChargeType type, decimal amount, DateOnly dueDate,
        Guid referenceId, string description, string sourceKey, Guid? requestId,
        string sourceFingerprint, Guid? actorId, DateTimeOffset now, string? period = null)
    {
        if (studentId == Guid.Empty || referenceId == Guid.Empty || !Enum.IsDefined(type)
            || requestId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Valid student, charge type and reference identifiers are required.");
        }

        ValidateAmount(amount);
        if (type == ChargeType.Meal)
        {
            throw new ArgumentException("Meal purchases are prepaid and cannot create an outstanding charge.");
        }
        if (dueDate == default)
        {
            throw new ArgumentException("Due date is required.");
        }

        Id = Guid.NewGuid();
        StudentId = studentId;
        Type = type;
        Amount = amount;
        DueDate = dueDate;
        ReferenceId = referenceId;
        Description = RequiredText(description, 1000);
        SourceKey = RequiredText(sourceKey, 150);
        SourceFingerprint = RequiredText(sourceFingerprint, 64);
        RequestId = requestId;
        RecordedByUserId = actorId;
        CreatedAtUtc = now.UtcDateTime;
        Period = period;
    }

    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public ChargeType Type { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly DueDate { get; private set; }
    public Guid ReferenceId { get; private set; }
    public string Description { get; private set; } = null!;
    public string SourceKey { get; private set; } = null!;
    public string SourceFingerprint { get; private set; } = null!;
    public Guid? RequestId { get; private set; }
    public Guid? RecordedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? Period { get; private set; }

    public string Status(DateOnly today)
    {
        return DueDate < today ? "OVERDUE" : "PENDING";
    }

    public static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("Amount must be positive, fit decimal(18,2), and have at most two decimal places.");
        }
    }

    private static string RequiredText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
        {
            throw new ArgumentException("Required charge text is missing or too long.");
        }

        return value.Trim();
    }
}
