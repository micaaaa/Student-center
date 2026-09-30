using NUnit.Framework;
using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Application.Services;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Tests;

public sealed class MealUsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [TestCase(0, 2026, 10)]
    [TestCase(-1, 2026, 10)]
    [TestCase(1, 2026, 0)]
    [TestCase(1, 2026, 13)]
    [TestCase(1, 9999, 12)]
    public void InvalidQuantityOrPeriodIsRejected(int quantity, int year, int month)
    {
        Assert.Throws<ArgumentException>(() =>
            new MealEntitlement(Guid.NewGuid(), "2026/2027", year, month, MealType.Lunch, quantity, Actor, Now));
    }

    [TestCase("2026-2027")]
    [TestCase("2026/2028")]
    [TestCase("2024/2025")]
    public void InvalidAcademicYearIsRejected(string academicYear)
    {
        Assert.Throws<ArgumentException>(() =>
            new MealEntitlement(Guid.NewGuid(), academicYear, 2026, 10, MealType.Lunch, 10, Actor, Now));
    }

    [Test]
    public void LastMealCanOnlyBeConsumedOnce()
    {
        var entitlement = Entitlement(1);
        var restaurant = new Restaurant("Restaurant", "Address");

        var consumption = entitlement.Consume(Guid.NewGuid(), restaurant, Actor, " CARD ", Now);

        Assert.That(entitlement.ConsumedQuantity, Is.EqualTo(1));
        Assert.That(entitlement.RemainingQuantity, Is.Zero);
        Assert.That(consumption.CardReference, Is.EqualTo("CARD"));
        Assert.That(consumption.StudentId, Is.EqualTo(entitlement.StudentId));
        Assert.Throws<FoodConflictException>(() =>
            entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, Now));
        Assert.That(entitlement.ConsumedQuantity, Is.EqualTo(1));
    }

    [Test]
    public void SuspendedEntitlementCannotBeConsumedAndCanBeResumed()
    {
        var entitlement = Entitlement(2);
        var restaurant = new Restaurant("Restaurant", "Address");
        entitlement.Update(2, MealEntitlementStatus.Suspended, Actor, Now);

        Assert.Throws<FoodConflictException>(() =>
            entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, Now));

        entitlement.Update(2, MealEntitlementStatus.Active, Actor, Now);
        entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, Now);
        Assert.That(entitlement.RemainingQuantity, Is.EqualTo(1));
    }

    [Test]
    public void EntitlementExpiresExactlyAtStartOfNextUtcMonth()
    {
        var entitlement = Entitlement(2);
        var restaurant = new Restaurant("Restaurant", "Address");
        var expiry = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, expiry.AddTicks(-1));

        Assert.That(entitlement.GetStatus(expiry), Is.EqualTo(MealEntitlementStatus.Expired));
        Assert.Throws<FoodConflictException>(() =>
            entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, expiry));
        Assert.Throws<FoodConflictException>(() =>
            entitlement.Update(3, MealEntitlementStatus.Active, Actor, expiry));
    }

    [Test]
    public void FutureEntitlementCannotBeConsumedEarly()
    {
        var entitlement = new MealEntitlement(Guid.NewGuid(), "2026/2027", 2026, 11,
            MealType.Lunch, 2, Actor, Now);

        Assert.Throws<FoodConflictException>(() =>
            entitlement.Consume(Guid.NewGuid(), new Restaurant("Restaurant", "Address"), Actor, null, Now));
        Assert.That(entitlement.ConsumedQuantity, Is.Zero);
    }

    [Test]
    public void InactiveRestaurantAndInvalidRequestDoNotSpendMeal()
    {
        var entitlement = Entitlement(2);
        var restaurant = new Restaurant("Restaurant", "Address");
        restaurant.Update(restaurant.Name, restaurant.Address, RestaurantStatus.Inactive);

        Assert.Throws<FoodConflictException>(() =>
            entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, Now));

        restaurant.Update(restaurant.Name, restaurant.Address, RestaurantStatus.Active);
        Assert.Throws<ArgumentException>(() => entitlement.Consume(Guid.Empty, restaurant, Actor, null, Now));
        Assert.Throws<ArgumentException>(() =>
            entitlement.Consume(Guid.NewGuid(), restaurant, Actor, new string('x', 101), Now));
        Assert.That(entitlement.ConsumedQuantity, Is.Zero);
    }

    [Test]
    public void QuotaCannotBeReducedBelowConsumedAmount()
    {
        var entitlement = Entitlement(3);
        var restaurant = new Restaurant("Restaurant", "Address");
        entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, Now);
        entitlement.Consume(Guid.NewGuid(), restaurant, Actor, null, Now);

        Assert.Throws<ArgumentException>(() =>
            entitlement.Update(1, MealEntitlementStatus.Active, Actor, Now));
        Assert.That(entitlement.AllowedQuantity, Is.EqualTo(3));

        entitlement.Update(2, MealEntitlementStatus.Active, Actor, Now);
        Assert.That(entitlement.RemainingQuantity, Is.Zero);
    }

    [Test]
    public void ExpiredCannotBeAssignedAsStoredStatus()
    {
        var entitlement = Entitlement(2);
        Assert.Throws<ArgumentException>(() =>
            entitlement.Update(2, MealEntitlementStatus.Expired, Actor, Now));
    }

    [Test]
    public async Task ReplayedRequestReturnsOriginalEvenAfterSuspensionWithoutSpendingAgain()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        var request = Request(repository);

        var first = await service.ConsumeAsync(request, Actor, default);
        repository.Entitlement.Update(2, MealEntitlementStatus.Suspended, Actor, Now);
        var replay = await service.ConsumeAsync(request, Actor, default);

        Assert.That(first.IsReplay, Is.False);
        Assert.That(replay.IsReplay, Is.True);
        Assert.That(replay.Consumption.Id, Is.EqualTo(first.Consumption.Id));
        Assert.That(repository.Entitlement.ConsumedQuantity, Is.EqualTo(1));
        Assert.That(repository.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RequestIdCannotBeReusedForDifferentPayload()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        var request = Request(repository);
        await service.ConsumeAsync(request, Actor, default);
        request.RestaurantId = Guid.NewGuid();

        Assert.ThrowsAsync<FoodConflictException>(async () =>
            await service.ConsumeAsync(request, Actor, default));
        Assert.That(repository.Entitlement.ConsumedQuantity, Is.EqualTo(1));
    }

    [Test]
    public async Task MyViewsUseStudentProfileIdAndNeverAccountId()
    {
        var repository = new MemoryRepository();
        var students = new StudentClient(repository.Entitlement.StudentId);
        var service = new MealUsageService(repository, students, new Clock());
        await service.ConsumeAsync(Request(repository), Actor, default);

        var rights = await service.GetMineAsync(2026, 10, default);
        var history = await service.GetMyHistoryAsync(2026, 10, 1, 50, default);
        Assert.That(rights.Single().StudentId, Is.EqualTo(students.Id));
        Assert.That(history.Single().StudentId, Is.EqualTo(students.Id));

        var otherStudent = new MealUsageService(repository, new StudentClient(Guid.NewGuid()), new Clock());
        Assert.That(await otherStudent.GetMineAsync(2026, 10, default), Is.Empty);
        Assert.That(await otherStudent.GetMyHistoryAsync(2026, 10, 1, 50, default), Is.Empty);
    }

    [Test]
    public void DuplicateEntitlementIsRejected()
    {
        var repository = new MemoryRepository();
        var request = new CreateMealEntitlementRequest
        {
            StudentId = repository.Entitlement.StudentId,
            AcademicYear = "2026/2027",
            Year = 2026,
            Month = 10,
            MealType = MealType.Lunch,
            AllowedQuantity = 2
        };

        Assert.ThrowsAsync<FoodConflictException>(async () =>
            await Service(repository).CreateAsync(request, Actor, default));
        Assert.That(repository.SaveCount, Is.Zero);
    }

    [Test]
    public void FailedStudentLookupDoesNotPersistEntitlement()
    {
        var repository = new MemoryRepository();
        var service = new MealUsageService(repository,
            new StudentClient(Guid.NewGuid(), unavailable: true), new Clock());

        Assert.ThrowsAsync<StudentLookupException>(async () => await service.CreateAsync(
            new CreateMealEntitlementRequest
            {
                StudentId = Guid.NewGuid(),
                AcademicYear = "2026/2027",
                Year = 2026,
                Month = 10,
                MealType = MealType.Lunch,
                AllowedQuantity = 5
            }, Actor, default));
        Assert.That(repository.SaveCount, Is.Zero);
    }

    [TestCase(0, 50)]
    [TestCase(1, 101)]
    [TestCase(int.MaxValue, 100)]
    public void InvalidPaginationIsRejected(int page, int pageSize)
    {
        var repository = new MemoryRepository();
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service(repository).GetHistoryAsync(Guid.NewGuid(), 2026, 10, page, pageSize, default));
    }

    private static MealEntitlement Entitlement(int quantity)
    {
        return new MealEntitlement(Guid.NewGuid(), "2026/2027", 2026, 10, MealType.Lunch, quantity, Actor, Now);
    }

    private static MealUsageService Service(MemoryRepository repository)
    {
        return new MealUsageService(repository, new StudentClient(repository.Entitlement.StudentId), new Clock());
    }

    private static ConsumeMealRequest Request(MemoryRepository repository)
    {
        return new ConsumeMealRequest
        {
            RequestId = Guid.NewGuid(),
            EntitlementId = repository.Entitlement.Id,
            RestaurantId = repository.Restaurant.Id
        };
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StudentClient(Guid id, bool unavailable = false) : IFoodStudentClient
    {
        public Guid Id => id;

        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct)
        {
            return Task.FromResult(id);
        }

        public Task EnsureActiveStudentAsync(Guid studentId, CancellationToken ct)
        {
            if (unavailable)
            {
                throw new StudentLookupException(503, "Unavailable.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class MemoryRepository : IMealUsageRepository
    {
        public MealEntitlement Entitlement { get; } = MealUsageTests.Entitlement(2);
        public Restaurant Restaurant { get; } = new("Restaurant", "Address");
        public List<MealConsumption> Consumptions { get; } = [];
        public int SaveCount { get; private set; }

        public Task<MealEntitlement?> GetEntitlementAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(id == Entitlement.Id ? Entitlement : null);
        }

        public Task<IReadOnlyCollection<MealEntitlement>> GetEntitlementsAsync(
            Guid studentId, int year, int month, CancellationToken ct)
        {
            IReadOnlyCollection<MealEntitlement> result = studentId == Entitlement.StudentId
                && year == Entitlement.Year && month == Entitlement.Month ? [Entitlement] : [];
            return Task.FromResult(result);
        }

        public Task<bool> EntitlementExistsAsync(
            Guid studentId, int year, int month, MealType type, CancellationToken ct)
        {
            return Task.FromResult(studentId == Entitlement.StudentId && year == Entitlement.Year
                && month == Entitlement.Month && type == Entitlement.MealType);
        }

        public Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(id == Restaurant.Id ? Restaurant : null);
        }

        public Task<MealConsumption?> GetConsumptionAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(Consumptions.SingleOrDefault(consumption => consumption.Id == id));
        }

        public Task<MealConsumption?> GetByRequestIdAsync(Guid requestId, CancellationToken ct)
        {
            return Task.FromResult(Consumptions.SingleOrDefault(consumption => consumption.RequestId == requestId));
        }

        public Task<IReadOnlyCollection<MealConsumption>> GetConsumptionsAsync(
            Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct)
        {
            IReadOnlyCollection<MealConsumption> result = Consumptions.Where(consumption =>
                consumption.StudentId == studentId && consumption.ConsumedAtUtc.Year == year
                && consumption.ConsumedAtUtc.Month == month).Skip((page - 1) * pageSize).Take(pageSize).ToArray();
            return Task.FromResult(result);
        }

        public Task AddEntitlementAsync(MealEntitlement entitlement, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task AddConsumptionAsync(MealConsumption consumption, CancellationToken ct)
        {
            Consumptions.Add(consumption);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            return action(ct);
        }
    }
}
