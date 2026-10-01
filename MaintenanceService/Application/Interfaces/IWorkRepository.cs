using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.Application.Interfaces;

public interface IWorkRepository
{
    Task<MaintenanceWorker?> GetWorkerAsync(Guid id, CancellationToken ct);
    Task<MaintenanceWorker?> GetWorkerByUserIdAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyCollection<MaintenanceWorker>> GetWorkersAsync(int page, int pageSize, CancellationToken ct);
    Task<bool> HasOpenAssignmentsAsync(Guid workerId, CancellationToken ct);
    Task AddWorkerAsync(MaintenanceWorker worker, CancellationToken ct);
    Task<MaintenanceRequest?> GetRequestAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<MaintenanceRequest>> GetAssignedRequestsAsync(
        Guid workerId, RequestStatus? status, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyCollection<MaintenanceAction>> GetActionsAsync(Guid requestId, int page, int pageSize, CancellationToken ct);
    Task AddActionAsync(MaintenanceAction action, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
