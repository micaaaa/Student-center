using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Domain.Exceptions;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.Infrastructure.Repositories;

public sealed class InventoryRepository(AccommodationDbContext db) : IInventoryRepository
{
    public async Task<IReadOnlyCollection<Dorm>> GetDormsAsync(CancellationToken ct) =>
        await db.Dorms.AsNoTracking().OrderBy(dorm => dorm.Name).ThenBy(dorm => dorm.Id).ToArrayAsync(ct);

    public Task<Dorm?> GetDormAsync(Guid id, CancellationToken ct) =>
        db.Dorms.SingleOrDefaultAsync(dorm => dorm.Id == id, ct);

    public async Task<IReadOnlyCollection<Room>> GetRoomsAsync(Guid dormId, CancellationToken ct) =>
        await db.Rooms.AsNoTracking().Where(room => room.DormId == dormId)
            .OrderBy(room => room.Floor).ThenBy(room => room.RoomNumber).ToArrayAsync(ct);

    public Task<Room?> GetRoomAsync(Guid id, CancellationToken ct) =>
        db.Rooms.SingleOrDefaultAsync(room => room.Id == id, ct);

    public Task<bool> RoomNumberExistsAsync(Guid dormId, string roomNumber, Guid? exceptId, CancellationToken ct) =>
        db.Rooms.AnyAsync(room => room.DormId == dormId && room.RoomNumber == roomNumber
            && (!exceptId.HasValue || room.Id != exceptId.Value), ct);

    public async Task<long> GetRoomCapacityAsync(Guid dormId, Guid? exceptId, CancellationToken ct) =>
        await db.Rooms.Where(room => room.DormId == dormId && (!exceptId.HasValue || room.Id != exceptId.Value))
            .SumAsync(room => (long?)room.Capacity, ct) ?? 0;

    public Task<bool> HasOccupiedRoomsAsync(Guid dormId, CancellationToken ct) =>
        db.Rooms.AnyAsync(room => room.DormId == dormId && room.OccupiedBeds > 0, ct);

    public async Task AddDormAsync(Dorm dorm, CancellationToken ct)
    {
        await db.Dorms.AddAsync(dorm, ct);
    }

    public async Task AddRoomAsync(Room room, CancellationToken ct)
    {
        await db.Rooms.AddAsync(room, ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await action(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AccommodationConflictException("The record has changed. Reload it before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AccommodationConflictException(
                "A duplicate room, eligibility event or active student assignment was detected. Reload before retrying.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            throw new AccommodationConflictException("Inventory changed concurrently. Reload before retrying.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new AccommodationConflictException("Inventory changed concurrently. Reload before retrying.");
        }
    }
}
