using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.Application.Services;

public sealed class MaintenanceWorkService(
    IWorkRepository repository, IStaffDirectoryClient directory, ICurrentStudentContext student, TimeProvider clock)
{
    public async Task<WorkerResponse> CreateWorkerAsync(CreateWorkerRequest request, CancellationToken ct)
    {
        var worker = new MaintenanceWorker(request.UserId, request.Name, request.Specialization);
        await directory.EnsureActiveStaffAsync(worker.UserId, ct);
        return await repository.InTransactionAsync(async token =>
        {
            if (await repository.GetWorkerByUserIdAsync(worker.UserId, token) is not null)
            {
                throw new MaintenanceConflictException("This account already has a worker profile.");
            }

            await repository.AddWorkerAsync(worker, token);
            await repository.SaveAsync(token);
            return ToResponse(worker);
        }, ct);
    }

    public async Task<WorkerResponse> UpdateWorkerAsync(Guid id, UpdateWorkerRequest request, CancellationToken ct)
    {
        var existing = await FindWorkerAsync(id, ct);
        if (request.IsActive)
        {
            await directory.EnsureActiveStaffAsync(existing.UserId, ct);
        }

        return await repository.InTransactionAsync(async token =>
        {
            var worker = await FindWorkerAsync(id, token);
            if (!request.IsActive && await repository.HasOpenAssignmentsAsync(id, token))
            {
                throw new MaintenanceConflictException("Reassign or resolve open tasks before deactivating this worker.");
            }

            worker.Update(request.Name, request.Specialization, request.IsActive);
            await repository.SaveAsync(token);
            return ToResponse(worker);
        }, ct);
    }

    public async Task<WorkerResponse> GetWorkerAsync(Guid id, CancellationToken ct)
    {
        return ToResponse(await FindWorkerAsync(id, ct));
    }

    public async Task<IReadOnlyCollection<WorkerResponse>> GetWorkersAsync(int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return (await repository.GetWorkersAsync(page, pageSize, ct)).Select(ToResponse).ToArray();
    }

    public async Task<WorkerResponse> GetMyWorkerAsync(MaintenanceActor actor, CancellationToken ct)
    {
        return ToResponse(await FindMyWorkerAsync(actor, ct));
    }

    public async Task<IReadOnlyCollection<MaintenanceRequestResponse>> GetMyTasksAsync(
        MaintenanceActor actor, RequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new ArgumentException("Unknown request status.");
        }

        var worker = await FindMyWorkerAsync(actor, ct);
        return (await repository.GetAssignedRequestsAsync(worker.Id, status, page, pageSize, ct))
            .Select(MaintenanceRequestResponse.From).ToArray();
    }

    public async Task<MaintenanceRequestResponse> GetTaskAsync(Guid id, MaintenanceActor actor, CancellationToken ct)
    {
        var request = await FindRequestAsync(id, ct);
        await EnsureAccessAsync(request, actor, false, ct);
        return MaintenanceRequestResponse.From(request);
    }

    public Task<MaintenanceRequestResponse> AssignAsync(
        Guid id, Guid workerId, MaintenanceActor actor, CancellationToken ct)
    {
        ValidateActor(actor);
        if (!actor.IsSupervisor)
        {
            throw new MaintenanceAccessException("Only maintenance supervisors can assign workers.");
        }

        return repository.InTransactionAsync(async token =>
        {
            var request = await FindRequestAsync(id, token);
            var worker = await FindWorkerAsync(workerId, token);
            var action = request.Assign(worker, actor.UserId, clock.GetUtcNow());
            await repository.AddActionAsync(action, token);
            await repository.SaveAsync(token);
            return MaintenanceRequestResponse.From(request);
        }, ct);
    }

    public Task<MaintenanceRequestResponse> StartAsync(Guid id, MaintenanceActor actor, CancellationToken ct)
    {
        return ProcessAsync(id, actor, request => request.Start(actor.UserId, clock.GetUtcNow()), ct);
    }

    public Task<MaintenanceRequestResponse> InterveneAsync(
        Guid id, string description, MaintenanceActor actor, CancellationToken ct)
    {
        return ProcessAsync(id, actor,
            request => request.RecordIntervention(actor.UserId, description, clock.GetUtcNow()), ct);
    }

    public Task<MaintenanceRequestResponse> ResolveAsync(
        Guid id, string description, MaintenanceActor actor, CancellationToken ct)
    {
        return ProcessAsync(id, actor, request => request.Resolve(actor.UserId, description, clock.GetUtcNow()), ct);
    }

    public async Task<IReadOnlyCollection<MaintenanceActionResponse>> GetActionsAsync(
        Guid id, MaintenanceActor actor, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var request = await FindRequestAsync(id, ct);
        await EnsureAccessAsync(request, actor, false, ct);
        return await ReadActionsAsync(id, page, pageSize, ct);
    }

    public async Task<IReadOnlyCollection<MaintenanceActionResponse>> GetMyStudentActionsAsync(
        Guid id, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var studentId = await student.GetStudentIdAsync(ct);
        var request = await FindRequestAsync(id, ct);
        if (request.StudentId != studentId)
        {
            throw new KeyNotFoundException("Maintenance request not found.");
        }

        return await ReadActionsAsync(id, page, pageSize, ct);
    }

    private Task<MaintenanceRequestResponse> ProcessAsync(Guid id, MaintenanceActor actor,
        Func<MaintenanceRequest, MaintenanceAction> change, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var request = await FindRequestAsync(id, token);
            await EnsureAccessAsync(request, actor, true, token);
            var action = change(request);
            await repository.AddActionAsync(action, token);
            await repository.SaveAsync(token);
            return MaintenanceRequestResponse.From(request);
        }, ct);
    }

    private async Task EnsureAccessAsync(
        MaintenanceRequest request, MaintenanceActor actor, bool modifying, CancellationToken ct)
    {
        ValidateActor(actor);
        if (actor.IsSupervisor)
        {
            return;
        }

        var worker = await repository.GetWorkerByUserIdAsync(actor.UserId, ct);
        if (worker is null || request.AssignedWorkerId != worker.Id)
        {
            if (modifying)
            {
                throw new MaintenanceAccessException("Only the assigned worker or a maintenance supervisor can process this request.");
            }

            throw new KeyNotFoundException("Maintenance request not found.");
        }

        if (modifying && !worker.IsActive)
        {
            throw new MaintenanceAccessException("An inactive worker cannot process requests.");
        }
    }

    private async Task<MaintenanceWorker> FindMyWorkerAsync(MaintenanceActor actor, CancellationToken ct)
    {
        ValidateActor(actor);
        return await repository.GetWorkerByUserIdAsync(actor.UserId, ct)
            ?? throw new KeyNotFoundException("Worker profile not found.");
    }

    private async Task<MaintenanceWorker> FindWorkerAsync(Guid id, CancellationToken ct)
    {
        return await repository.GetWorkerAsync(id, ct) ?? throw new KeyNotFoundException("Worker not found.");
    }

    private async Task<MaintenanceRequest> FindRequestAsync(Guid id, CancellationToken ct)
    {
        return await repository.GetRequestAsync(id, ct) ?? throw new KeyNotFoundException("Maintenance request not found.");
    }

    private async Task<IReadOnlyCollection<MaintenanceActionResponse>> ReadActionsAsync(
        Guid id, int page, int pageSize, CancellationToken ct)
    {
        return (await repository.GetActionsAsync(id, page, pageSize, ct)).Select(action =>
            new MaintenanceActionResponse(action.Id, action.WorkerId, action.Type, action.Description, action.CreatedAtUtc))
            .ToArray();
    }

    private static void ValidateActor(MaintenanceActor actor)
    {
        if (actor.UserId == Guid.Empty)
        {
            throw new MaintenanceAccessException("A valid authenticated staff identity is required.");
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        }
    }

    private static WorkerResponse ToResponse(MaintenanceWorker worker)
    {
        return new WorkerResponse(worker.Id, worker.UserId, worker.Name, worker.Specialization, worker.IsActive);
    }
}
