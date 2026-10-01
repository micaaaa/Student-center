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

    [TestCase("")]
    [TestCase("   ")]
    public void BlankTitleIsRejected(string title)
    {
        Assert.Throws<ArgumentException>(() => Request(title));
    }

    [Test]
    public void InvalidDescriptionAndPriorityAreRejected()
    {
        var category = new MaintenanceCategory("Electricity", null);
        Assert.Throws<ArgumentException>(() => new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), category, "Title", new string('x', 4001), RequestPriority.Medium, Now));
        Assert.Throws<ArgumentException>(() => new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), category, "Title", "Description", (RequestPriority)99, Now));
    }

    [Test]
    public void InactiveCategoryCannotReceiveNewRequest()
    {
        var category = new MaintenanceCategory("Electricity", null);
        category.Update(category.Name, null, false);
        Assert.Throws<MaintenanceConflictException>(() => new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), category, "Title", "Description", RequestPriority.Medium, Now));
    }

    [Test]
    public void AcceptanceRecordsReviewerAndPreventsAnotherInitialDecision()
    {
        var request = Request();
        var actor = Guid.NewGuid();
        request.Accept(actor, Now.AddMinutes(1));

        Assert.That(request.Status, Is.EqualTo(RequestStatus.Accepted));
        Assert.That(request.ReviewedByUserId, Is.EqualTo(actor));
        Assert.That(request.ReviewedAtUtc, Is.EqualTo(Now.AddMinutes(1).UtcDateTime));
        Assert.Throws<MaintenanceConflictException>(() => request.Accept(actor, Now));
        Assert.Throws<MaintenanceConflictException>(() => request.Reject(actor, "Reason", Now));
        Assert.Throws<MaintenanceConflictException>(() => request.Cancel(Now));
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
    public void CancellationClosesRequest()
    {
        var request = Request();
        request.Cancel(Now.AddMinutes(1));

        Assert.That(request.CancelledAtUtc, Is.EqualTo(Now.AddMinutes(1).UtcDateTime));
        Assert.Throws<MaintenanceConflictException>(() => request.Cancel(Now));
        Assert.Throws<MaintenanceConflictException>(() => request.Accept(Guid.NewGuid(), Now));
        Assert.Throws<MaintenanceConflictException>(() => request.ChangePriority(RequestPriority.Low, Now));
    }

    [Test]
    public void PriorityCanChangeWhileAccepted()
    {
        var request = Request();
        request.Accept(Guid.NewGuid(), Now);
        request.ChangePriority(RequestPriority.Urgent, Now.AddMinutes(2));
        Assert.That(request.Priority, Is.EqualTo(RequestPriority.Urgent));
    }

    [Test]
    public void EmptyReviewerCannotChangeStatus()
    {
        var request = Request();
        Assert.Throws<ArgumentException>(() => request.Accept(Guid.Empty, Now));
        Assert.That(request.Status, Is.EqualTo(RequestStatus.Submitted));
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
    public void FailedAccommodationLookupDoesNotSaveRequest()
    {
        var store = new Store();
        var context = new StudentContext { Unavailable = true };

        Assert.ThrowsAsync<ServiceLookupException>(async () =>
            await Service(store, context).SubmitAsync(Submit(store), default));
        Assert.That(store.Requests, Is.Empty);
        Assert.That(store.Saves, Is.Zero);
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

    [Test]
    public async Task HistoryDoesNotRequireCurrentAccommodationAfterMoveOut()
    {
        var store = new Store();
        var context = new StudentContext();
        var service = Service(store, context);
        var request = await service.SubmitAsync(Submit(store), default);
        context.Unavailable = true;

        Assert.That(await service.GetMineAsync(null, 1, 50, default), Has.Count.EqualTo(1));
        Assert.That((await service.GetMineAsync(request.Id, default)).Id, Is.EqualTo(request.Id));
        Assert.That((await service.CancelMineAsync(request.Id, default)).Status, Is.EqualTo(RequestStatus.Cancelled));
    }

    [Test]
    public async Task DuplicateCategoryAndInactiveVisibilityAreHandled()
    {
        var store = new Store();
        var service = Service(store, new StudentContext());
        Assert.ThrowsAsync<MaintenanceConflictException>(async () =>
            await service.SaveCategoryAsync(null, new CategoryRequest { Name = " plumbing " }, default));

        await service.SaveCategoryAsync(store.Category.Id,
            new CategoryRequest { Name = "Plumbing", IsActive = false }, default);

        Assert.That(await service.GetCategoriesAsync(false, default), Is.Empty);
        Assert.That(await service.GetCategoriesAsync(true, default), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ListSupportsStatusFilterAndPagination()
    {
        var store = new Store();
        var service = Service(store, new StudentContext());
        var first = await service.SubmitAsync(Submit(store), default);
        await service.SubmitAsync(Submit(store), default);
        await service.AcceptAsync(first.Id, Guid.NewGuid(), default);

        Assert.That(await service.GetRequestsAsync(null, RequestStatus.Accepted, 1, 50, default),
            Has.Count.EqualTo(1));
        Assert.That(await service.GetMineAsync(null, 1, 1, default), Has.Count.EqualTo(1));
        Assert.That(await service.GetMineAsync(null, 3, 1, default), Is.Empty);
    }

    [TestCase(0, 50)]
    [TestCase(1, 101)]
    [TestCase(int.MaxValue, 100)]
    public void InvalidPaginationIsRejected(int page, int size)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service(new Store(), new StudentContext()).GetRequestsAsync(null, null, page, size, default));
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
