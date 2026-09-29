using StudentCenter.AccommodationService.Domain.Entities;

namespace StudentCenter.AccommodationService.Application.Interfaces;

public interface IInventoryRepository
{
    Task<IReadOnlyCollection<Dorm>> GetDormsAsync(CancellationToken ct);
    Task<Dorm?> GetDormAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<Room>> GetRoomsAsync(Guid dormId, CancellationToken ct);
    Task<Room?> GetRoomAsync(Guid id, CancellationToken ct);
    Task<bool> RoomNumberExistsAsync(Guid dormId, string roomNumber, Guid? exceptId, CancellationToken ct);
    Task<long> GetRoomCapacityAsync(Guid dormId, Guid? exceptId, CancellationToken ct);
    Task<bool> HasOccupiedRoomsAsync(Guid dormId, CancellationToken ct);
    Task AddDormAsync(Dorm dorm, CancellationToken ct);
    Task AddRoomAsync(Room room, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
