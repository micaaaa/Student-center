using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.Application.Interfaces;

public interface IMaintenanceRepository
{
    Task<IReadOnlyCollection<MaintenanceCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct);
    Task<MaintenanceCategory?> GetCategoryAsync(Guid id, CancellationToken ct);
    Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);
    Task AddCategoryAsync(MaintenanceCategory category, CancellationToken ct);
    Task<MaintenanceRequest?> GetRequestAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<MaintenanceRequest>> GetRequestsAsync(
        Guid? studentId, RequestStatus? status, int page, int pageSize, CancellationToken ct);
    Task AddRequestAsync(MaintenanceRequest request, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
