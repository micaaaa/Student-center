using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using StudentCenter.AccommodationService.API.Controllers;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Application.Services;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Domain.Enums;
using StudentCenter.AccommodationService.Domain.Exceptions;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.Tests;

public sealed class InventoryTests
{
    private Store store = null!;
    private InventoryService service = null!;

    [SetUp]
    public void SetUp()
    {
        store = new Store();
        service = new InventoryService(store);
    }

    private static DormRequest DormRequest(int capacity = 10, DormStatus status = DormStatus.Active) => new()
    {
        Name = "  Studentski dom  ",
        Address = "  Studentska 1  ",
        City = " Novi Sad ",
        Category = " I ",
        Capacity = capacity,
        Status = status
    };

    private static RoomRequest RoomRequest(string number = " a101 ", int capacity = 2, RoomStatus status = RoomStatus.Available) => new()
    {
        RoomNumber = number,
        Floor = 1,
        Capacity = capacity,
        Status = status
    };

    [Test]
    public async Task CreateReadAndUpdateDormAndRoom()
    {
        var dorm = await service.CreateDormAsync(DormRequest(), default);
        Assert.That(dorm.Name, Is.EqualTo("Studentski dom"));
        Assert.That(dorm.Status, Is.EqualTo("ACTIVE"));
        Assert.That((await service.GetDormsAsync(default)).Single().Id, Is.EqualTo(dorm.Id));
        var room = await service.CreateRoomAsync(dorm.Id, RoomRequest(), default);
        Assert.That(room.RoomNumber, Is.EqualTo("A101"));
        Assert.That(room.OccupiedBeds, Is.Zero);
        Assert.That(room.Status, Is.EqualTo("AVAILABLE"));
        Assert.That((await service.GetRoomsAsync(dorm.Id, default)).Single().Id, Is.EqualTo(room.Id));
        await service.UpdateDormAsync(dorm.Id, DormRequest(12), default);
        await service.UpdateRoomAsync(room.Id, RoomRequest("a102", 3), default);
        Assert.That((await service.GetDormAsync(dorm.Id, default)).Capacity, Is.EqualTo(12));
        var updated = await service.GetRoomAsync(room.Id, default);
        Assert.That(updated.RoomNumber, Is.EqualTo("A102"));
        Assert.That(updated.Capacity, Is.EqualTo(3));
    }

    [Test]
    public async Task RoomNumberMustBeUniqueOnlyWithinDorm()
    {
        var first = await service.CreateDormAsync(DormRequest(), default);
        var second = await service.CreateDormAsync(DormRequest(), default);
        await service.CreateRoomAsync(first.Id, RoomRequest("A101"), default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.CreateRoomAsync(first.Id, RoomRequest(" a101 "), default));
        await service.CreateRoomAsync(second.Id, RoomRequest("a101"), default);
        Assert.That(store.Rooms.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task RoomCannotBeRenamedToExistingNumber()
    {
        var dorm = await service.CreateDormAsync(DormRequest(), default);
        await service.CreateRoomAsync(dorm.Id, RoomRequest("1"), default);
        var other = await service.CreateRoomAsync(dorm.Id, RoomRequest("2"), default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.UpdateRoomAsync(other.Id, RoomRequest("1"), default));
        Assert.That((await service.GetRoomAsync(other.Id, default)).RoomNumber, Is.EqualTo("2"));
    }

    [Test]
    public async Task RoomCapacityCannotExceedDormCapacityOnCreateOrUpdate()
    {
        var dorm = await service.CreateDormAsync(DormRequest(4), default);
        var room = await service.CreateRoomAsync(dorm.Id, RoomRequest("1", 3), default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.CreateRoomAsync(dorm.Id, RoomRequest("2", 2), default));
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.UpdateRoomAsync(room.Id, RoomRequest("1", 5), default));
        Assert.That(store.Rooms.Single().Capacity, Is.EqualTo(3));
    }

    [Test]
    public async Task RoomUpdateDoesNotCountItsOldCapacityTwice()
    {
        var dorm = await service.CreateDormAsync(DormRequest(4), default);
        var room = await service.CreateRoomAsync(dorm.Id, RoomRequest("1", 3), default);
        Assert.That((await service.UpdateRoomAsync(room.Id, RoomRequest("1", 4), default)).Capacity, Is.EqualTo(4));
    }

    [Test]
    public async Task DormCapacityCannotBeReducedBelowExistingRooms()
    {
        var dorm = await service.CreateDormAsync(DormRequest(10), default);
        await service.CreateRoomAsync(dorm.Id, RoomRequest("1", 3), default);
        await service.CreateRoomAsync(dorm.Id, RoomRequest("2", 3, RoomStatus.Inactive), default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.UpdateDormAsync(dorm.Id, DormRequest(5), default));
        Assert.That(store.Dorms.Single().Capacity, Is.EqualTo(10));
    }

    [Test]
    public async Task InactiveDormCannotReceiveNewOrAvailableRooms()
    {
        var dorm = await service.CreateDormAsync(DormRequest(), default);
        var room = await service.CreateRoomAsync(dorm.Id, RoomRequest(), default);
        await service.UpdateDormAsync(dorm.Id, DormRequest(status: DormStatus.Inactive), default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.CreateRoomAsync(dorm.Id, RoomRequest("2"), default));
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.UpdateRoomAsync(room.Id, RoomRequest(), default));
        Assert.That((await service.UpdateRoomAsync(room.Id, RoomRequest(status: RoomStatus.Maintenance), default)).Status,
            Is.EqualTo("MAINTENANCE"));
    }

    [Test]
    public async Task OccupiedDormCannotBeDeactivated()
    {
        var dorm = await service.CreateDormAsync(DormRequest(), default);
        await service.CreateRoomAsync(dorm.Id, RoomRequest(), default);
        store.Rooms.Single().ReserveBed();
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.UpdateDormAsync(dorm.Id, DormRequest(status: DormStatus.Inactive), default));
        Assert.That(store.Dorms.Single().Status, Is.EqualTo(DormStatus.Active));
    }

    [Test]
    public void FullStatusFollowsOccupancyAndCannotBeManuallyAssigned()
    {
        var room = new Room(Guid.NewGuid(), "1", 1, 2);
        room.ReserveBed();
        Assert.That(room.Status, Is.EqualTo(RoomStatus.Available));
        room.ReserveBed();
        Assert.That(room.Status, Is.EqualTo(RoomStatus.Full));
        Assert.Throws<AccommodationConflictException>(() => room.ReserveBed());
        room.ReleaseBed();
        Assert.That(room.Status, Is.EqualTo(RoomStatus.Available));
        Assert.Throws<ArgumentException>(() => room.Update("1", 1, 2, RoomStatus.Full));
        room.ReleaseBed();
        Assert.Throws<AccommodationConflictException>(() => room.ReleaseBed());
    }

    [TestCase(RoomStatus.Inactive)]
    [TestCase(RoomStatus.Maintenance)]
    public void UnavailableRoomCannotReserveBed(RoomStatus status)
    {
        var room = new Room(Guid.NewGuid(), "1", 1, 2, status);
        Assert.Throws<AccommodationConflictException>(() => room.ReserveBed());
        Assert.That(room.OccupiedBeds, Is.Zero);
    }

    [Test]
    public void OccupiedRoomCannotShrinkBelowOccupancyOrBecomeUnavailable()
    {
        var room = new Room(Guid.NewGuid(), "1", 1, 2);
        room.ReserveBed();
        room.ReserveBed();
        Assert.Throws<AccommodationConflictException>(() => room.Update("changed", 0, 1, RoomStatus.Available));
        Assert.Throws<AccommodationConflictException>(() => room.Update("changed", 0, 2, RoomStatus.Maintenance));
        Assert.Throws<AccommodationConflictException>(() => room.Update("changed", 0, 2, RoomStatus.Inactive));
        Assert.That(room.RoomNumber, Is.EqualTo("1"));
        room.Update("1", 1, 3, RoomStatus.Available);
        Assert.That(room.Status, Is.EqualTo(RoomStatus.Available));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveCapacityIsRejected(int capacity)
    {
        Assert.Throws<ArgumentException>(() => new Dorm("Name", "Address", "City", "Category", capacity));
        Assert.Throws<ArgumentException>(() => new Room(Guid.NewGuid(), "1", 1, capacity));
    }

    [Test]
    public void MissingFieldsAndInvalidStatusesAreRejectedWithoutMutation()
    {
        var dorm = new Dorm("Name", "Address", "City", "Category", 10);
        Assert.Throws<ArgumentException>(() => dorm.Update(" ", "Address", "City", "Category", 10, DormStatus.Active));
        Assert.Throws<ArgumentException>(() => dorm.Update("Name", "Address", "City", "Category", 10, (DormStatus)999));
        Assert.That(dorm.Name, Is.EqualTo("Name"));
        Assert.Throws<ArgumentException>(() => new Room(Guid.NewGuid(), " ", 1, 2));
        Assert.Throws<ArgumentException>(() => new Room(Guid.Empty, "1", 1, 2));
        Assert.Throws<ArgumentException>(() => new Room(Guid.NewGuid(), "1", 1, 2, (RoomStatus)999));
    }

    [Test]
    public async Task UnknownIdsReturnNotFoundAndEmptyListsAreAllowed()
    {
        Assert.That(await service.GetDormsAsync(default), Is.Empty);
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetDormAsync(Guid.NewGuid(), default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRoomAsync(Guid.NewGuid(), default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetRoomsAsync(Guid.NewGuid(), default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateRoomAsync(Guid.NewGuid(), RoomRequest(), default));
        var dorm = await service.CreateDormAsync(DormRequest(), default);
        Assert.That(await service.GetRoomsAsync(dorm.Id, default), Is.Empty);
    }

    [Test]
    public async Task ControllersReturnCreatedBadRequestConflictAndNotFound()
    {
        var dorms = new DormsController(service);
        var rooms = new RoomsController(service);
        Assert.That(await dorms.Create(DormRequest(), default), Is.TypeOf<CreatedAtActionResult>());
        Assert.That(await dorms.Create(DormRequest(0), default), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(await rooms.Get(Guid.NewGuid(), default), Is.TypeOf<NotFoundObjectResult>());
        var dormId = store.Dorms.Single().Id;
        Assert.That(await rooms.Create(dormId, RoomRequest(), default), Is.TypeOf<CreatedAtActionResult>());
        Assert.That(await rooms.Create(dormId, RoomRequest(), default), Is.TypeOf<ConflictObjectResult>());
    }

    [TestCase("STUDENT", false, false)]
    [TestCase("STUDENT", true, false)]
    [TestCase("STAFF", false, false)]
    [TestCase("ADMIN", false, false)]
    [TestCase("STAFF", true, true)]
    [TestCase("ADMIN", true, true)]
    public async Task StaffRoleAndAccommodationPermissionAreBothRequired(string role, bool permission, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy("ManageAccommodation", policy =>
            policy.RequireAuthenticatedUser().RequireClaim("permission", "ManageAccommodation")));
        using var provider = services.BuildServiceProvider();
        foreach (var type in new[] { typeof(DormsController), typeof(RoomsController) })
        {
            var metadata = type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<IAuthorizeData>();
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            var claims = new List<Claim> { new(ClaimTypes.Role, role) };
            if (permission)
                claims.Add(new Claim("permission", "ManageAccommodation"));
            var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null, policy!);
            Assert.That(result.Succeeded, Is.EqualTo(allowed));
        }
    }

    [Test]
    public void EfModelHasUniqueRoomNumbersAndCapacityConstraints()
    {
        using var db = new AccommodationDbContext(new DbContextOptionsBuilder<AccommodationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var room = db.Model.FindEntityType(typeof(Room))!;
        Assert.That(room.GetIndexes().Any(index => index.IsUnique
            && index.Properties.Select(item => item.Name).SequenceEqual(new[] { "DormId", "RoomNumber" })), Is.True);
        Assert.That(room.FindProperty(nameof(Room.RowVersion))!.IsConcurrencyToken, Is.True);
        Assert.That(db.Model.GetEntityTypes().Select(entity => entity.ClrType), Is.EquivalentTo(new[] { typeof(Dorm), typeof(Room) }));
    }

    private sealed class Store : IInventoryRepository
    {
        public List<Dorm> Dorms { get; } = [];
        public List<Room> Rooms { get; } = [];

        public Task<IReadOnlyCollection<Dorm>> GetDormsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Dorm>>(Dorms.ToArray());

        public Task<Dorm?> GetDormAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Dorms.SingleOrDefault(dorm => dorm.Id == id));

        public Task<IReadOnlyCollection<Room>> GetRoomsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Room>>(Rooms.Where(room => room.DormId == id).ToArray());

        public Task<Room?> GetRoomAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Rooms.SingleOrDefault(room => room.Id == id));

        public Task<bool> RoomNumberExistsAsync(Guid dormId, string number, Guid? exceptId, CancellationToken ct) =>
            Task.FromResult(Rooms.Any(room => room.DormId == dormId && room.RoomNumber == number && room.Id != exceptId));

        public Task<long> GetRoomCapacityAsync(Guid dormId, Guid? exceptId, CancellationToken ct) =>
            Task.FromResult(Rooms.Where(room => room.DormId == dormId && room.Id != exceptId).Sum(room => (long)room.Capacity));

        public Task<bool> HasOccupiedRoomsAsync(Guid dormId, CancellationToken ct) =>
            Task.FromResult(Rooms.Any(room => room.DormId == dormId && room.OccupiedBeds > 0));

        public Task AddDormAsync(Dorm dorm, CancellationToken ct)
        {
            Dorms.Add(dorm);
            return Task.CompletedTask;
        }

        public Task AddRoomAsync(Room room, CancellationToken ct)
        {
            Rooms.Add(room);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
}
