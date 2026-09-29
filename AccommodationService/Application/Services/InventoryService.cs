using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Domain.Enums;
using StudentCenter.AccommodationService.Domain.Exceptions;

namespace StudentCenter.AccommodationService.Application.Services;

public sealed class InventoryService(IInventoryRepository repository)
{
    public async Task<IReadOnlyCollection<DormResponse>> GetDormsAsync(CancellationToken ct) =>
        (await repository.GetDormsAsync(ct)).Select(MapDorm).ToArray();

    public async Task<DormResponse> GetDormAsync(Guid id, CancellationToken ct) => MapDorm(await FindDormAsync(id, ct));

    public Task<DormResponse> CreateDormAsync(DormRequest request, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var dorm = new Dorm(request.Name, request.Address, request.City, request.Category, request.Capacity);
            dorm.Update(request.Name, request.Address, request.City, request.Category, request.Capacity, request.Status);
            await repository.AddDormAsync(dorm, token);
            await repository.SaveAsync(token);
            return MapDorm(dorm);
        }, ct);

    public Task<DormResponse> UpdateDormAsync(Guid id, DormRequest request, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var dorm = await FindDormAsync(id, token);
            if (request.Capacity < await repository.GetRoomCapacityAsync(id, null, token))
                throw new AccommodationConflictException("Dorm capacity cannot be lower than the total room capacity.");
            if (request.Status == DormStatus.Inactive && await repository.HasOccupiedRoomsAsync(id, token))
                throw new AccommodationConflictException("An occupied dorm cannot be deactivated.");

            dorm.Update(request.Name, request.Address, request.City, request.Category, request.Capacity, request.Status);
            await repository.SaveAsync(token);
            return MapDorm(dorm);
        }, ct);

    public async Task<IReadOnlyCollection<RoomResponse>> GetRoomsAsync(Guid dormId, CancellationToken ct)
    {
        await FindDormAsync(dormId, ct);
        return (await repository.GetRoomsAsync(dormId, ct)).Select(MapRoom).ToArray();
    }

    public async Task<RoomResponse> GetRoomAsync(Guid id, CancellationToken ct) =>
        MapRoom(await FindRoomAsync(id, ct));

    public Task<RoomResponse> CreateRoomAsync(Guid dormId, RoomRequest request, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var dorm = await FindDormAsync(dormId, token);
            if (dorm.Status != DormStatus.Active)
                throw new AccommodationConflictException("Activate the dorm before adding rooms.");
            var room = new Room(dormId, request.RoomNumber, request.Floor, request.Capacity, request.Status);
            await ValidateRoomAsync(dorm, room.RoomNumber, request.Capacity, null, token);
            await repository.AddRoomAsync(room, token);
            await repository.SaveAsync(token);
            return MapRoom(room);
        }, ct);

    public Task<RoomResponse> UpdateRoomAsync(Guid id, RoomRequest request, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var room = await FindRoomAsync(id, token);
            var dorm = await FindDormAsync(room.DormId, token);
            if (dorm.Status == DormStatus.Inactive && request.Status == RoomStatus.Available)
                throw new AccommodationConflictException("Activate the dorm before making a room available.");
            if (string.IsNullOrWhiteSpace(request.RoomNumber))
                throw new ArgumentException("Room number is required.");

            await ValidateRoomAsync(dorm, request.RoomNumber.Trim().ToUpperInvariant(), request.Capacity, room.Id, token);
            room.Update(request.RoomNumber, request.Floor, request.Capacity, request.Status);
            await repository.SaveAsync(token);
            return MapRoom(room);
        }, ct);

    private async Task ValidateRoomAsync(
        Dorm dorm, string number, int capacity, Guid? exceptId, CancellationToken ct)
    {
        if (await repository.RoomNumberExistsAsync(dorm.Id, number, exceptId, ct))
            throw new AccommodationConflictException("Room number already exists in this dorm.");
        var total = await repository.GetRoomCapacityAsync(dorm.Id, exceptId, ct);
        if (total + capacity > dorm.Capacity)
            throw new AccommodationConflictException("The total room capacity would exceed dorm capacity.");
    }

    private async Task<Dorm> FindDormAsync(Guid id, CancellationToken ct) =>
        await repository.GetDormAsync(id, ct) ?? throw new KeyNotFoundException("Dorm was not found.");

    private async Task<Room> FindRoomAsync(Guid id, CancellationToken ct) =>
        await repository.GetRoomAsync(id, ct) ?? throw new KeyNotFoundException("Room was not found.");

    private static DormResponse MapDorm(Dorm dorm) => new(
        dorm.Id, dorm.Name, dorm.Address, dorm.City, dorm.Category, dorm.Capacity, dorm.Status.ToString().ToUpperInvariant());

    private static RoomResponse MapRoom(Room room) => new(
        room.Id, room.DormId, room.RoomNumber, room.Floor, room.Capacity, room.OccupiedBeds, room.Status.ToString().ToUpperInvariant());
}
