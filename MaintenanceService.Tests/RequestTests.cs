using NUnit.Framework;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.Tests;

public sealed class RequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void InactiveCategoryCannotReceiveNewRequest()
    {
        var category = new MaintenanceCategory("Electricity", null);
        category.Update(category.Name, null, false);
        Assert.Throws<MaintenanceConflictException>(() => new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), category, "Title", "Description", RequestPriority.Medium, Now));
    }

    [Test]
    public void RejectionRequiresReasonAndClosesRequest()
    {
        var request = Request();
        Assert.Throws<ArgumentException>(() => request.Reject(Guid.NewGuid(), " ", Now));
        Assert.That(request.Status, Is.EqualTo(RequestStatus.Submitted));
        request.Reject(Guid.NewGuid(), " Duplicate report ", Now);

        Assert.That(request.RejectionReason, Is.EqualTo("Duplicate report"));
        Assert.That(request.Status, Is.EqualTo(RequestStatus.Rejected));
        Assert.Throws<MaintenanceConflictException>(() => request.ChangePriority(RequestPriority.High, Now));
    }

    [Test]
    public async Task SubmissionUsesCurrentStudentAndAccommodation()
    {
        var store = new Store();
        var context = new StudentContext();
        var service = Service(store, context);

        var response = await service.SubmitAsync(Submit(store), default);

        Assert.That(response.StudentId, Is.EqualTo(context.StudentId));
        Assert.That(response.RoomId, Is.EqualTo(context.Accommodation.RoomId));
        Assert.That(response.AccommodationId, Is.EqualTo(context.Accommodation.Id));
        Assert.That(response.Status, Is.EqualTo(RequestStatus.Submitted));
        Assert.That(store.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task AnotherStudentCannotReadOrCancelRequest()
    {
        var store = new Store();
        var owner = new StudentContext();
        var ownService = Service(store, owner);
        var request = await ownService.SubmitAsync(Submit(store), default);
        var stranger = Service(store, new StudentContext());

        Assert.That(await stranger.GetMineAsync(null, 1, 50, default), Is.Empty);
        Assert.ThrowsAsync<KeyNotFoundException>(async () => await stranger.GetMineAsync(request.Id, default));
        Assert.ThrowsAsync<KeyNotFoundException>(async () => await stranger.CancelMineAsync(request.Id, default));
        Assert.That((await ownService.GetMineAsync(request.Id, default)).Status, Is.EqualTo(RequestStatus.Submitted));
    }

    private static MaintenanceRequest Request(string title = "Water leak")
    {
        return new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new MaintenanceCategory("Plumbing", null), title, "Description", RequestPriority.Medium, Now);
    }

    private static SubmitMaintenanceRequest Submit(Store store)
    {
        return new SubmitMaintenanceRequest
        {
            CategoryId = store.Category.Id,
            Title = "Water leak",
            Description = "Leak below sink",
            Priority = RequestPriority.High
        };
    }

    private static MaintenanceRequestService Service(Store store, StudentContext context)
    {
        return new MaintenanceRequestService(store, context, new Clock());
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StudentContext : ICurrentStudentContext
    {
        public Guid StudentId { get; } = Guid.NewGuid();
        public CurrentAccommodation Accommodation { get; } = new(Guid.NewGuid(), Guid.NewGuid());
        public bool Unavailable { get; set; }

        public Task<Guid> GetStudentIdAsync(CancellationToken ct)
        {
            return Task.FromResult(StudentId);
        }

        public Task<CurrentAccommodation> GetActiveAccommodationAsync(CancellationToken ct)
        {
            if (Unavailable)
            {
                throw new ServiceLookupException(503, "Unavailable.");
            }

            return Task.FromResult(Accommodation);
        }
    }

    private sealed class Store : IMaintenanceRepository
    {
        public MaintenanceCategory Category { get; } = new("Plumbing", null);
        public List<MaintenanceRequest> Requests { get; } = [];
        public int Saves { get; private set; }

        public Task<IReadOnlyCollection<MaintenanceCategory>> GetCategoriesAsync(bool includeInactive, CancellationToken ct)
        {
            IReadOnlyCollection<MaintenanceCategory> result = includeInactive || Category.IsActive ? [Category] : [];
            return Task.FromResult(result);
        }

        public Task<MaintenanceCategory?> GetCategoryAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(id == Category.Id ? Category : null);
        }

        public Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
        {
            return Task.FromResult(string.Equals(Category.Name, name, StringComparison.OrdinalIgnoreCase)
                && Category.Id != exceptId);
        }

        public Task AddCategoryAsync(MaintenanceCategory category, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task<MaintenanceRequest?> GetRequestAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(Requests.SingleOrDefault(request => request.Id == id));
        }

        public Task<IReadOnlyCollection<MaintenanceRequest>> GetRequestsAsync(
            Guid? studentId, RequestStatus? status, int page, int pageSize, CancellationToken ct)
        {
            IReadOnlyCollection<MaintenanceRequest> result = Requests.Where(request =>
                (!studentId.HasValue || request.StudentId == studentId)
                && (!status.HasValue || request.Status == status))
                .OrderByDescending(request => request.CreatedAtUtc).ThenBy(request => request.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToArray();
            return Task.FromResult(result);
        }

        public Task AddRequestAsync(MaintenanceRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            return action(ct);
        }
    }
}
