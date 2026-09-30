using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Application.Services;

public sealed class MealPurchaseService(
    IMealPurchaseRepository repository, IFoodStudentClient students, TimeProvider clock)
{
    public Task<PurchaseResult> PurchaseAsync(MealPurchaseRequest request, Guid actorId, CancellationToken ct)
    {
        MealPurchase.Validate(request.RequestId, request.EntitlementId, request.Quantity, request.UnitPrice, actorId);
        return repository.InTransactionAsync(async token =>
        {
            var previous = await repository.GetByRequestIdAsync(request.RequestId, token);
            if (previous is not null)
            {
                if (previous.EntitlementId != request.EntitlementId || previous.Quantity != request.Quantity
                    || previous.UnitPrice != request.UnitPrice)
                {
                    throw new FoodConflictException("Request ID was already used for a different purchase.");
                }

                return new PurchaseResult(ToResponse(previous), true);
            }

            var entitlement = await repository.GetEntitlementAsync(request.EntitlementId, token)
                ?? throw new KeyNotFoundException("Meal entitlement not found.");
            var purchase = entitlement.Purchase(request.RequestId, request.Quantity, request.UnitPrice,
                actorId, clock.GetUtcNow());
            await repository.AddAsync(purchase, token);
            await repository.AddEventAsync(FoodOutboxMessage.From(purchase), token);
            await repository.SaveAsync(token);
            return new PurchaseResult(ToResponse(purchase), false);
        }, ct);
    }

    public async Task<MealPurchaseResponse> GetAsync(Guid id, CancellationToken ct)
    {
        return ToResponse(await FindAsync(id, ct));
    }

    public async Task<MealPurchaseResponse> GetMineAsync(Guid id, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        var purchase = await FindAsync(id, ct);
        if (purchase.StudentId != studentId)
        {
            throw new KeyNotFoundException("Meal purchase not found.");
        }

        return ToResponse(purchase);
    }

    public async Task<IReadOnlyCollection<MealPurchaseResponse>> GetHistoryAsync(
        Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student identifier is required.");
        }

        MealEntitlement.ValidatePeriod(year, month);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        }

        var purchases = await repository.GetHistoryAsync(studentId, year, month, page, pageSize, ct);
        return purchases.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<MealPurchaseResponse>> GetMyHistoryAsync(
        int year, int month, int page, int pageSize, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return await GetHistoryAsync(studentId, year, month, page, pageSize, ct);
    }

    private async Task<MealPurchase> FindAsync(Guid id, CancellationToken ct)
    {
        return await repository.GetAsync(id, ct)
            ?? throw new KeyNotFoundException("Meal purchase not found.");
    }

    private static MealPurchaseResponse ToResponse(MealPurchase purchase)
    {
        return new MealPurchaseResponse(purchase.Id, purchase.RequestId, purchase.EntitlementId,
            purchase.StudentId, purchase.AcademicYear, purchase.Year, purchase.Month, purchase.MealType,
            purchase.Quantity, purchase.UnitPrice, purchase.Amount, purchase.PurchasedAtUtc);
    }
}
