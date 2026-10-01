using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.Application.Services;

public sealed class MaintenanceRequestService(
    IMaintenanceRepository repository, ICurrentStudentContext student, TimeProvider clock)
{
    public async Task<IReadOnlyCollection<CategoryResponse>> GetCategoriesAsync(bool includeInactive, CancellationToken ct)
    {
        return (await repository.GetCategoriesAsync(includeInactive, ct)).Select(ToResponse).ToArray();
    }

    public Task<CategoryResponse> SaveCategoryAsync(Guid? id, CategoryRequest request, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var category = id.HasValue
                ? await repository.GetCategoryAsync(id.Value, token)
                    ?? throw new KeyNotFoundException("Category not found.")
                : new MaintenanceCategory(request.Name, request.Description);
            category.Update(request.Name, request.Description, request.IsActive);
            if (await repository.CategoryNameExistsAsync(category.Name, id, token))
            {
                throw new MaintenanceConflictException("A category with this name already exists.");
            }

            if (!id.HasValue)
            {
                await repository.AddCategoryAsync(category, token);
            }

            await repository.SaveAsync(token);
            return ToResponse(category);
        }, ct);
    }

    public async Task<MaintenanceRequestResponse> SubmitAsync(SubmitMaintenanceRequest request, CancellationToken ct)
    {
        var studentId = await student.GetStudentIdAsync(ct);
        var accommodation = await student.GetActiveAccommodationAsync(ct);
        return await repository.InTransactionAsync(async token =>
        {
            var category = await repository.GetCategoryAsync(request.CategoryId, token)
                ?? throw new KeyNotFoundException("Category not found.");
            var entity = new MaintenanceRequest(studentId, accommodation.Id, accommodation.RoomId, category,
                request.Title, request.Description, request.Priority, clock.GetUtcNow());
            await repository.AddRequestAsync(entity, token);
            await repository.SaveAsync(token);
            return ToResponse(entity);
        }, ct);
    }

    public async Task<MaintenanceRequestResponse> GetAsync(Guid id, CancellationToken ct)
    {
        return ToResponse(await FindAsync(id, ct));
    }

    public async Task<MaintenanceRequestResponse> GetMineAsync(Guid id, CancellationToken ct)
    {
        var studentId = await student.GetStudentIdAsync(ct);
        var request = await FindAsync(id, ct);
        EnsureOwner(request, studentId);
        return ToResponse(request);
    }

    public async Task<IReadOnlyCollection<MaintenanceRequestResponse>> GetRequestsAsync(
        Guid? studentId, RequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        if (studentId == Guid.Empty || (status.HasValue && !Enum.IsDefined(status.Value))
            || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentException("Invalid student, status or pagination. Page size must be between 1 and 100.");
        }

        return (await repository.GetRequestsAsync(studentId, status, page, pageSize, ct))
            .Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<MaintenanceRequestResponse>> GetMineAsync(
        RequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var studentId = await student.GetStudentIdAsync(ct);
        return await GetRequestsAsync(studentId, status, page, pageSize, ct);
    }

    public async Task<MaintenanceRequestResponse> CancelMineAsync(Guid id, CancellationToken ct)
    {
        var studentId = await student.GetStudentIdAsync(ct);
        return await repository.InTransactionAsync(async token =>
        {
            var request = await FindAsync(id, token);
            EnsureOwner(request, studentId);
            request.Cancel(clock.GetUtcNow());
            await repository.SaveAsync(token);
            return ToResponse(request);
        }, ct);
    }

    public Task<MaintenanceRequestResponse> AcceptAsync(Guid id, Guid actorId, CancellationToken ct)
    {
        return ChangeAsync(id, request => request.Accept(actorId, clock.GetUtcNow()), ct);
    }

    public Task<MaintenanceRequestResponse> RejectAsync(Guid id, Guid actorId, string reason, CancellationToken ct)
    {
        return ChangeAsync(id, request => request.Reject(actorId, reason, clock.GetUtcNow()), ct);
    }

    public Task<MaintenanceRequestResponse> ChangePriorityAsync(Guid id, RequestPriority priority, CancellationToken ct)
    {
        return ChangeAsync(id, request => request.ChangePriority(priority, clock.GetUtcNow()), ct);
    }

    private Task<MaintenanceRequestResponse> ChangeAsync(Guid id, Action<MaintenanceRequest> change, CancellationToken ct)
    {
        return repository.InTransactionAsync(async token =>
        {
            var request = await FindAsync(id, token);
            change(request);
            await repository.SaveAsync(token);
            return ToResponse(request);
        }, ct);
    }

    private async Task<MaintenanceRequest> FindAsync(Guid id, CancellationToken ct)
    {
        return await repository.GetRequestAsync(id, ct) ?? throw new KeyNotFoundException("Maintenance request not found.");
    }

    private static void EnsureOwner(MaintenanceRequest request, Guid studentId)
    {
        if (request.StudentId != studentId)
        {
            throw new KeyNotFoundException("Maintenance request not found.");
        }
    }

    private static CategoryResponse ToResponse(MaintenanceCategory category)
    {
        return new CategoryResponse(category.Id, category.Name, category.Description, category.IsActive);
    }

    private static MaintenanceRequestResponse ToResponse(MaintenanceRequest request)
    {
        return new MaintenanceRequestResponse(request.Id, request.StudentId, request.AccommodationId,
            request.RoomId, request.CategoryId, request.Title, request.Description, request.Priority, request.Status,
            request.CreatedAtUtc, request.UpdatedAtUtc, request.ReviewedAtUtc, request.RejectionReason, request.CancelledAtUtc);
    }
}

