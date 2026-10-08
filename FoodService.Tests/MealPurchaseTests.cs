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
