using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Domain.Entities;
using StudentCenter.FoodService.Domain.Enums;
using StudentCenter.FoodService.Domain.Exceptions;
using StudentCenter.FoodService.Infrastructure.Persistence;

namespace StudentCenter.FoodService.Infrastructure.Repositories;

public sealed class FoodCatalogRepository(FoodDbContext db) : IFoodCatalogRepository
{
    public async Task<IReadOnlyCollection<Restaurant>> GetRestaurantsAsync(
        bool includeInactive, CancellationToken ct)
    {
        return await db.Restaurants.AsNoTracking()
            .Where(restaurant => includeInactive || restaurant.Status == RestaurantStatus.Active)
            .OrderBy(restaurant => restaurant.Name).ThenBy(restaurant => restaurant.Id)
            .ToArrayAsync(ct);
    }

    public Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct)
    {
        return db.Restaurants.SingleOrDefaultAsync(restaurant => restaurant.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<Menu>> GetMenusAsync(
        Guid restaurantId, DateOnly from, DateOnly to, bool includeDrafts, CancellationToken ct)
    {
        return await db.Menus.AsNoTracking().Include(menu => menu.Meals)
            .Where(menu => menu.RestaurantId == restaurantId && menu.Date >= from && menu.Date <= to
                && (includeDrafts || menu.Status == MenuStatus.Published))
            .OrderBy(menu => menu.Date).ToArrayAsync(ct);
    }

    public Task<Menu?> GetMenuAsync(Guid id, CancellationToken ct)
    {
        return db.Menus.Include(menu => menu.Meals).SingleOrDefaultAsync(menu => menu.Id == id, ct);
    }

    public Task<bool> MenuExistsAsync(Guid restaurantId, DateOnly date, Guid? exceptId, CancellationToken ct)
    {
        return db.Menus.AnyAsync(menu => menu.RestaurantId == restaurantId && menu.Date == date
            && (!exceptId.HasValue || menu.Id != exceptId.Value), ct);
    }

    public async Task AddRestaurantAsync(Restaurant restaurant, CancellationToken ct)
    {
        await db.Restaurants.AddAsync(restaurant, ct);
    }

    public async Task AddMenuAsync(Menu menu, CancellationToken ct)
    {
        await db.Menus.AddAsync(menu, ct);
    }

    public Task SaveAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }

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
            throw new FoodConflictException("The record changed concurrently. Reload before retrying.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new FoodConflictException("A menu already exists for this restaurant and date.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            throw new FoodConflictException("The catalog changed concurrently. Reload before retrying.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw new FoodConflictException("The catalog changed concurrently. Reload before retrying.");
        }
    }
}
