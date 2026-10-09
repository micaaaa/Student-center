using System.Text.RegularExpressions;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Domain.Entities;

public sealed class MealEntitlement
{
    private MealEntitlement()
    {
    }

    public MealEntitlement(Guid studentId, string academicYear, int year, int month,
        MealType mealType, Guid actorId, DateTimeOffset now)
    {
        if (studentId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Student and staff identifiers are required.");
        }

        ValidatePeriod(year, month);
        var cleanYear = academicYear?.Trim();
        if (cleanYear is null || !Regex.IsMatch(cleanYear, @"^[0-9]{4}/[0-9]{4}$"))
        {
            throw new ArgumentException("Academic year must use the YYYY/YYYY format.");
        }

        var firstYear = int.Parse(cleanYear[..4]);
        var secondYear = int.Parse(cleanYear[5..]);
        if (firstYear < 1 || secondYear != firstYear + 1 || (year != firstYear && year != secondYear))
        {
            throw new ArgumentException("Academic year must contain consecutive years and include the calendar year.");
        }

        if (!Enum.IsDefined(mealType))
        {
            throw new ArgumentException("Unknown meal type.");
        }

        Id = Guid.NewGuid();
        StudentId = studentId;
        AcademicYear = cleanYear;
        Year = year;
        Month = month;
        MealType = mealType;
        CreatedByUserId = actorId;
        CreatedAtUtc = now.UtcDateTime;
        Update(MealEntitlementStatus.Active, actorId, now);
    }

    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public string AcademicYear { get; private set; } = null!;
    public int Year { get; private set; }
    public int Month { get; private set; }
    public MealType MealType { get; private set; }
    public int AllowedQuantity { get; private set; }
    public int ConsumedQuantity { get; private set; }
    public int RemainingQuantity => AllowedQuantity - ConsumedQuantity;
    public MealEntitlementStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public MealEntitlementStatus GetStatus(DateTimeOffset now)
    {
        return DateOnly.FromDateTime(now.UtcDateTime) >= PeriodStart().AddMonths(1)
            ? MealEntitlementStatus.Expired
            : Status;
    }

    public void Update(MealEntitlementStatus status, Guid actorId, DateTimeOffset now)
    {
        if (status is not (MealEntitlementStatus.Active or MealEntitlementStatus.Suspended))
        {
            throw new ArgumentException("Only Active or Suspended can be assigned; expiration is automatic.");
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Staff identifier is required.");
        }

        if (GetStatus(now) == MealEntitlementStatus.Expired)
        {
            throw new FoodConflictException("Expired meal entitlement cannot be changed.");
        }

        Status = status;
        UpdatedByUserId = actorId;
        UpdatedAtUtc = now.UtcDateTime;
    }

    public MealConsumption Consume(Guid requestId, Restaurant restaurant, Guid actorId,
        string? cardReference, DateTimeOffset now)
    {
        if (GetStatus(now) != MealEntitlementStatus.Active
            || DateOnly.FromDateTime(now.UtcDateTime) < PeriodStart())
        {
            throw new FoodConflictException("Meal entitlement is not active for the current month.");
        }

        if (restaurant.Status != RestaurantStatus.Active)
        {
            throw new FoodConflictException("Meals cannot be consumed at an inactive restaurant.");
        }

        if (RemainingQuantity <= 0)
        {
            throw new FoodConflictException("No meals remain for this entitlement.");
        }

        var consumption = new MealConsumption(requestId, this, restaurant.Id, actorId, cardReference, now);
        ConsumedQuantity++;
        return consumption;
    }

    public MealPurchase Purchase(Guid requestId, int quantity, decimal unitPrice, Guid actorId, DateTimeOffset now)
    {
        var purchase = new MealPurchase(requestId, this, quantity, unitPrice, actorId, now);
        if (GetStatus(now) != MealEntitlementStatus.Active)
        {
            throw new FoodConflictException("Purchases require an active, unexpired meal entitlement.");
        }

        if ((long)AllowedQuantity + quantity > int.MaxValue)
        {
            throw new ArgumentException("Purchased quantity exceeds the supported entitlement limit.");
        }

        AllowedQuantity += quantity;
        UpdatedByUserId = actorId;
        UpdatedAtUtc = now.UtcDateTime;
        return purchase;
    }

    public static void ValidatePeriod(int year, int month)
    {
        if (year is < 1 or > 9998 || month is < 1 or > 12)
        {
            throw new ArgumentException("Specify a calendar year from 1 to 9998 and a month from 1 to 12.");
        }
    }

    private DateOnly PeriodStart()
    {
        return new DateOnly(Year, Month, 1);
    }
}
