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
