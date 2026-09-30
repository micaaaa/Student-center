using NUnit.Framework;
using StudentCenter.FoodService.Application.DTOs;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Application.Services;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Tests;

public sealed class MealPurchaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [Test]
    public void PurchaseAddsQuantityAndPreservesConsumedCount()
    {
        var entitlement = Entitlement();
        entitlement.Consume(Guid.NewGuid(), new Restaurant("Restaurant", "Address"), Actor, null, Now);

        var purchase = entitlement.Purchase(Guid.NewGuid(), 3, 120.25m, Actor, Now);

        Assert.Multiple(() =>
        {
            Assert.That(entitlement.AllowedQuantity, Is.EqualTo(5));
            Assert.That(entitlement.ConsumedQuantity, Is.EqualTo(1));
            Assert.That(entitlement.RemainingQuantity, Is.EqualTo(4));
            Assert.That(purchase.Amount, Is.EqualTo(360.75m));
            Assert.That(purchase.UnitPrice, Is.EqualTo(120.25m));
            Assert.That(purchase.StudentId, Is.EqualTo(entitlement.StudentId));
            Assert.That(purchase.Year, Is.EqualTo(entitlement.Year));
            Assert.That(purchase.Month, Is.EqualTo(entitlement.Month));
            Assert.That(purchase.RecordedByUserId, Is.EqualTo(Actor));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidQuantityDoesNotAlterBalance(int quantity)
    {
        var entitlement = Entitlement();
        Assert.Throws<ArgumentException>(() => entitlement.Purchase(Guid.NewGuid(), quantity, 100, Actor, Now));
        Assert.That(entitlement.AllowedQuantity, Is.EqualTo(2));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(1.234)]
    [TestCase(100000000)]
    public void InvalidPriceDoesNotAlterBalance(decimal price)
    {
        var entitlement = Entitlement();
        Assert.Throws<ArgumentException>(() => entitlement.Purchase(Guid.NewGuid(), 1, price, Actor, Now));
        Assert.That(entitlement.AllowedQuantity, Is.EqualTo(2));
    }

    [Test]
    public void QuantityAndAmountOverflowAreRejected()
    {
        var entitlement = Entitlement();
        Assert.Throws<ArgumentException>(() =>
            entitlement.Purchase(Guid.NewGuid(), int.MaxValue, 1, Actor, Now));
        Assert.Throws<ArgumentException>(() =>
            entitlement.Purchase(Guid.NewGuid(), int.MaxValue, 99999999.99m, Actor, Now));
        Assert.That(entitlement.AllowedQuantity, Is.EqualTo(2));
    }

    [Test]
    public void EmptyIdentifiersDoNotAlterBalance()
    {
        var entitlement = Entitlement();
        Assert.Throws<ArgumentException>(() => entitlement.Purchase(Guid.Empty, 1, 100, Actor, Now));
        Assert.Throws<ArgumentException>(() => entitlement.Purchase(Guid.NewGuid(), 1, 100, Guid.Empty, Now));
        Assert.That(entitlement.AllowedQuantity, Is.EqualTo(2));
    }

    [Test]
    public void SuspendedAndExpiredEntitlementsCannotBeToppedUp()
    {
        var entitlement = Entitlement();
        entitlement.Update(2, MealEntitlementStatus.Suspended, Actor, Now);
        Assert.Throws<FoodConflictException>(() => entitlement.Purchase(Guid.NewGuid(), 1, 100, Actor, Now));

        entitlement.Update(2, MealEntitlementStatus.Active, Actor, Now);
        Assert.Throws<FoodConflictException>(() =>
            entitlement.Purchase(Guid.NewGuid(), 1, 100, Actor, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.That(entitlement.AllowedQuantity, Is.EqualTo(2));
    }

    [Test]
    public void FutureMonthCanBePurchasedButNotConsumedEarly()
    {
        var entitlement = new MealEntitlement(Guid.NewGuid(), "2026/2027", 2026, 11,
            MealType.Lunch, 2, Actor, Now);

        var purchase = entitlement.Purchase(Guid.NewGuid(), 3, 0.10m, Actor, Now);

        Assert.That(purchase.Month, Is.EqualTo(11));
        Assert.That(purchase.Amount, Is.EqualTo(0.30m));
        Assert.Throws<FoodConflictException>(() =>
            entitlement.Consume(Guid.NewGuid(), new Restaurant("Restaurant", "Address"), Actor, null, Now));
    }

    [Test]
    public async Task ReplayedPurchaseReturnsOriginalWithoutAddingQuantityAgain()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        var request = Request(repository);
        var first = await service.PurchaseAsync(request, Actor, default);
        repository.Entitlement.Update(5, MealEntitlementStatus.Suspended, Actor, Now);

        var replay = await service.PurchaseAsync(request, Actor, default);

        Assert.That(first.IsReplay, Is.False);
        Assert.That(replay.IsReplay, Is.True);
        Assert.That(replay.Purchase, Is.EqualTo(first.Purchase));
        Assert.That(repository.Entitlement.AllowedQuantity, Is.EqualTo(5));
        Assert.That(repository.Purchases, Has.Count.EqualTo(1));
        Assert.That(repository.Events, Has.Count.EqualTo(1));
        Assert.That(repository.Events.Single().PurchaseId, Is.EqualTo(first.Purchase.Id));
        Assert.That(repository.SaveCount, Is.EqualTo(1));
    }

    [TestCase("quantity")]
    [TestCase("price")]
    [TestCase("entitlement")]
    public async Task RequestIdCannotBeReusedWithDifferentPayload(string field)
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        var request = Request(repository);
        await service.PurchaseAsync(request, Actor, default);
        switch (field)
        {
            case "quantity":
                request.Quantity++;
                break;
            case "price":
                request.UnitPrice++;
                break;
            case "entitlement":
                request.EntitlementId = Guid.NewGuid();
                break;
        }

        Assert.ThrowsAsync<FoodConflictException>(async () =>
            await service.PurchaseAsync(request, Actor, default));
        Assert.That(repository.Entitlement.AllowedQuantity, Is.EqualTo(5));
        Assert.That(repository.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public void MissingEntitlementDoesNotSavePurchase()
    {
        var repository = new MemoryRepository();
        var request = Request(repository);
        request.EntitlementId = Guid.NewGuid();

        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await Service(repository).PurchaseAsync(request, Actor, default));
        Assert.That(repository.SaveCount, Is.Zero);
        Assert.That(repository.Purchases, Is.Empty);
    }

    [Test]
    public async Task StudentCanOnlyReadOwnPurchases()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        var purchase = await service.PurchaseAsync(Request(repository), Actor, default);

        Assert.That((await service.GetMineAsync(purchase.Purchase.Id, default)).Id,
            Is.EqualTo(purchase.Purchase.Id));
        Assert.That(await service.GetMyHistoryAsync(2026, 10, 1, 50, default), Has.Count.EqualTo(1));

        var other = new MealPurchaseService(repository, new StudentClient(Guid.NewGuid()), new Clock());
        Assert.That(await other.GetMyHistoryAsync(2026, 10, 1, 50, default), Is.Empty);
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await other.GetMineAsync(purchase.Purchase.Id, default));
    }

    [Test]
    public async Task HistoryFiltersEntitlementMonthAndPaginates()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        await service.PurchaseAsync(Request(repository), Actor, default);
        await service.PurchaseAsync(Request(repository), Actor, default);

        Assert.That(await service.GetMyHistoryAsync(2026, 11, 1, 50, default), Is.Empty);
        Assert.That(await service.GetMyHistoryAsync(2026, 10, 1, 1, default), Has.Count.EqualTo(1));
        Assert.That(await service.GetMyHistoryAsync(2026, 10, 2, 1, default), Has.Count.EqualTo(1));
        Assert.That(await service.GetMyHistoryAsync(2026, 10, 3, 1, default), Is.Empty);
    }

    [TestCase(0, 50)]
    [TestCase(1, 101)]
    [TestCase(int.MaxValue, 100)]
    public void InvalidPaginationIsRejected(int page, int size)
    {
        var repository = new MemoryRepository();
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service(repository).GetHistoryAsync(Guid.NewGuid(), 2026, 10, page, size, default));
    }

    [Test]
    public void InvalidPeriodAndStudentAreRejected()
    {
        var service = Service(new MemoryRepository());
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetHistoryAsync(Guid.Empty, 2026, 10, 1, 50, default));
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetHistoryAsync(Guid.NewGuid(), 2026, 13, 1, 50, default));
    }

    private static MealEntitlement Entitlement()
    {
        return new MealEntitlement(Guid.NewGuid(), "2026/2027", 2026, 10, MealType.Lunch, 2, Actor, Now);
    }

    private static MealPurchaseRequest Request(MemoryRepository repository)
    {
        return new MealPurchaseRequest
        {
            RequestId = Guid.NewGuid(),
            EntitlementId = repository.Entitlement.Id,
            Quantity = 3,
            UnitPrice = 120.25m
        };
    }

    private static MealPurchaseService Service(MemoryRepository repository)
    {
        return new MealPurchaseService(repository, new StudentClient(repository.Entitlement.StudentId), new Clock());
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StudentClient(Guid id) : IFoodStudentClient
    {
        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct)
        {
            return Task.FromResult(id);
        }

        public Task EnsureActiveStudentAsync(Guid studentId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class MemoryRepository : IMealPurchaseRepository
    {
        public MealEntitlement Entitlement { get; } = MealPurchaseTests.Entitlement();
        public List<MealPurchase> Purchases { get; } = [];
        public List<FoodOutboxMessage> Events { get; } = [];
        public int SaveCount { get; private set; }

        public Task<MealEntitlement?> GetEntitlementAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(id == Entitlement.Id ? Entitlement : null);
        }

        public Task<MealPurchase?> GetAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(Purchases.SingleOrDefault(purchase => purchase.Id == id));
        }

        public Task<MealPurchase?> GetByRequestIdAsync(Guid requestId, CancellationToken ct)
        {
            return Task.FromResult(Purchases.SingleOrDefault(purchase => purchase.RequestId == requestId));
        }

        public Task<IReadOnlyCollection<MealPurchase>> GetHistoryAsync(
            Guid studentId, int year, int month, int page, int pageSize, CancellationToken ct)
        {
            IReadOnlyCollection<MealPurchase> result = Purchases.Where(purchase =>
                purchase.StudentId == studentId && purchase.Year == year && purchase.Month == month)
                .OrderByDescending(purchase => purchase.PurchasedAtUtc).ThenBy(purchase => purchase.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToArray();
            return Task.FromResult(result);
        }

        public Task AddAsync(MealPurchase purchase, CancellationToken ct)
        {
            Purchases.Add(purchase);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddEventAsync(FoodOutboxMessage message, CancellationToken ct)
        {
            Events.Add(message);
            return Task.CompletedTask;
        }

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            return action(ct);
        }
    }
}
