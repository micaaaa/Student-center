using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Application.Services;

public sealed class MealUsageService(
    IMealUsageRepository repository, IFoodStudentClient students, TimeProvider clock)
{
    public async Task<MealEntitlementResponse> CreateAsync(
        CreateMealEntitlementRequest request, Guid actorId, CancellationToken ct)
    {
        var entitlement = new MealEntitlement(request.StudentId, request.AcademicYear,
            request.Year, request.Month, request.MealType, request.AllowedQuantity, actorId, clock.GetUtcNow());
        await students.EnsureActiveStudentAsync(request.StudentId, ct);

        return await repository.InTransactionAsync(async token =>
        {
            if (await repository.EntitlementExistsAsync(
                request.StudentId, request.Year, request.Month, request.MealType, token))
            {
                throw new FoodConflictException("An entitlement already exists for this student, month and meal type.");
            }

            await repository.AddEntitlementAsync(entitlement, token);
            await repository.SaveAsync(token);
            return ToResponse(entitlement);
        }, ct);
    }

    public Task<MealEntitlementResponse> UpdateAsync(
        Guid id, UpdateMealEntitlementRequest request, Guid actorId, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var entitlement = await FindEntitlementAsync(id, token);
            entitlement.Update(request.AllowedQuantity, request.Status, actorId, clock.GetUtcNow());
            await repository.SaveAsync(token);
            return ToResponse(entitlement);
        }, ct);
    }

    public async Task<MealEntitlementResponse> GetAsync(Guid id, CancellationToken ct)
    {
        return ToResponse(await FindEntitlementAsync(id, ct));
    }

    public async Task<IReadOnlyCollection<MealEntitlementResponse>> GetForStudentAsync(
        Guid studentId, int year, int month, CancellationToken ct)
    {
        ValidateStudentAndPeriod(studentId, year, month);
        var entitlements = await repository.GetEntitlementsAsync(studentId, year, month, ct);
        return entitlements.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<MealEntitlementResponse>> GetMineAsync(
        int year, int month, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return await GetForStudentAsync(studentId, year, month, ct);
    }

    public Task<ConsumptionResult> ConsumeAsync(ConsumeMealRequest request, Guid actorId, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.EntitlementId == Guid.Empty
            || request.RestaurantId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Request, entitlement, restaurant and staff identifiers are required.");
        }

        var cardReference = MealConsumption.NormalizeCardReference(request.CardReference);
        return repository.InTransactionAsync(async token =>
        {
            var previous = await repository.GetByRequestIdAsync(request.RequestId, token);
            if (previous is not null)
            {
                if (previous.EntitlementId != request.EntitlementId || previous.RestaurantId != request.RestaurantId
                    || previous.CardReference != cardReference)
                {
                    throw new FoodConflictException("Request ID was already used for a different consumption.");
                }

                return new ConsumptionResult(ToResponse(previous), true);
            }

            var entitlement = await FindEntitlementAsync(request.EntitlementId, token);
            var restaurant = await repository.GetRestaurantAsync(request.RestaurantId, token)
                ?? throw new KeyNotFoundException("Restaurant not found.");
            var consumption = entitlement.Consume(request.RequestId, restaurant, actorId, cardReference, clock.GetUtcNow());
            await repository.AddConsumptionAsync(consumption, token);
            await repository.SaveAsync(token);
            return new ConsumptionResult(ToResponse(consumption), false);
        }, ct);
    }

    public async Task<MealConsumptionResponse> GetConsumptionAsync(Guid id, CancellationToken ct)
    {
        var consumption = await repository.GetConsumptionAsync(id, ct)
            ?? throw new KeyNotFoundException("Meal consumption not found.");
        return ToResponse(consumption);
    }

    public async Task<IReadOnlyCollection<MealConsumptionResponse>> GetHistoryAsync(
        Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct)
    {
        ValidateStudentAndPeriod(studentId, year, month);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        }

        var consumptions = await repository.GetConsumptionsAsync(studentId, year, month, page, pageSize, ct);
        return consumptions.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<MealConsumptionResponse>> GetMyHistoryAsync(
        int year, int month, int page, int pageSize, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return await GetHistoryAsync(studentId, year, month, page, pageSize, ct);
    }

    private async Task<MealEntitlement> FindEntitlementAsync(Guid id, CancellationToken ct)
    {
        return await repository.GetEntitlementAsync(id, ct)
            ?? throw new KeyNotFoundException("Meal entitlement not found.");
    }

    private static void ValidateStudentAndPeriod(Guid studentId, int year, int month)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student identifier is required.");
        }

        MealEntitlement.ValidatePeriod(year, month);
    }

    private MealEntitlementResponse ToResponse(MealEntitlement entitlement)
    {
        return new MealEntitlementResponse(entitlement.Id, entitlement.StudentId, entitlement.AcademicYear,
            entitlement.Year, entitlement.Month, entitlement.MealType, entitlement.AllowedQuantity,
            entitlement.ConsumedQuantity, entitlement.RemainingQuantity, entitlement.GetStatus(clock.GetUtcNow()));
    }

    private static MealConsumptionResponse ToResponse(MealConsumption consumption)
    {
        return new MealConsumptionResponse(consumption.Id, consumption.RequestId, consumption.EntitlementId,
            consumption.StudentId, consumption.RestaurantId, consumption.MealType, consumption.ConsumedAtUtc);
    }
}
