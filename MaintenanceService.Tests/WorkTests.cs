using NUnit.Framework;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Domain.Enums;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.Tests;

public sealed class WorkTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly MaintenanceActor Supervisor = new(Guid.NewGuid(), true);

    [Test]
    public void OnlyAcceptedRequestsCanReceiveFirstAssignment()
    {
        var request = NewRequest();
        var worker = Worker();
        Assert.Throws<MaintenanceConflictException>(() => request.Assign(worker, Supervisor.UserId, Now));
        request.Accept(Supervisor.UserId, Now);
        var action = request.Assign(worker, Supervisor.UserId, Now);

        Assert.That(request.Status, Is.EqualTo(RequestStatus.Assigned));
        Assert.That(request.AssignedWorkerId, Is.EqualTo(worker.Id));
        Assert.That(action.WorkerId, Is.EqualTo(worker.Id));
        Assert.Throws<MaintenanceConflictException>(() => request.Assign(worker, Supervisor.UserId, Now));
    }

    [Test]
    public void InactiveWorkerCannotBeAssigned()
    {
        var request = Accepted();
        var worker = Worker();
        worker.Update(worker.Name, worker.Specialization, false);
        Assert.Throws<MaintenanceConflictException>(() => request.Assign(worker, Supervisor.UserId, Now));
        Assert.That(request.AssignedWorkerId, Is.Null);
    }

    [Test]
    public void ResolutionRequiresStartAndNonBlankIntervention()
    {
        var request = Accepted();
        var worker = Worker();
        request.Assign(worker, Supervisor.UserId, Now);

        Assert.Throws<MaintenanceConflictException>(() => request.Resolve(worker.UserId, "Fixed", Now));
        request.Start(worker.UserId, Now);
        Assert.Throws<ArgumentException>(() => request.Resolve(worker.UserId, " ", Now));
        Assert.That(request.Status, Is.EqualTo(RequestStatus.InProgress));

        var action = request.Resolve(worker.UserId, " Replaced pipe ", Now.AddMinutes(5));
        Assert.That(request.Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(request.ResolutionDescription, Is.EqualTo("Replaced pipe"));
        Assert.That(request.ResolvedByUserId, Is.EqualTo(worker.UserId));
        Assert.That(action.Type, Is.EqualTo(MaintenanceActionType.Resolved));
        Assert.Throws<MaintenanceConflictException>(() => request.Resolve(worker.UserId, "Again", Now));
        Assert.Throws<MaintenanceConflictException>(() => request.RecordIntervention(worker.UserId, "Extra", Now));
        Assert.Throws<MaintenanceConflictException>(() => request.ChangePriority(RequestPriority.High, Now));
        Assert.Throws<MaintenanceConflictException>(() => request.Assign(Worker(), Supervisor.UserId, Now));
    }

    [Test]
    public void ReassignmentPreservesPreviousActionAndRequiresNewStart()
    {
        var request = Accepted();
        var first = Worker();
        request.Assign(first, Supervisor.UserId, Now);
        request.Start(first.UserId, Now);
        var intervention = request.RecordIntervention(first.UserId, "Inspected pipe", Now);
        var second = Worker();
        request.Assign(second, Supervisor.UserId, Now.AddMinutes(5));

        Assert.That(request.Status, Is.EqualTo(RequestStatus.Assigned));
        Assert.That(request.StartedAtUtc, Is.Null);
        Assert.That(intervention.WorkerId, Is.EqualTo(first.Id));
        Assert.Throws<MaintenanceConflictException>(() => request.RecordIntervention(second.UserId, "Work", Now));
    }

    [Test]
    public void InvalidActionDoesNotModifyRequest()
    {
        var request = Accepted();
        request.Assign(Worker(), Supervisor.UserId, Now);
        Assert.Throws<ArgumentException>(() => request.Start(Guid.Empty, Now));
        Assert.That(request.Status, Is.EqualTo(RequestStatus.Assigned));
        request.Start(Supervisor.UserId, Now);
        Assert.Throws<ArgumentException>(() =>
            request.RecordIntervention(Supervisor.UserId, new string('x', 4001), Now));
    }

    [Test]
    public async Task AssignedWorkerProcessesRequestWithoutSupervisorPermission()
    {
        var store = new Store();
        var service = Service(store);
        var actor = new MaintenanceActor(store.Workers[0].UserId, false);
        await service.AssignAsync(store.Request.Id, store.Workers[0].Id, Supervisor, default);
        await service.StartAsync(store.Request.Id, actor, default);
        await service.InterveneAsync(store.Request.Id, "Inspected", actor, default);
        await service.ResolveAsync(store.Request.Id, "Replaced pipe", actor, default);

        Assert.That(store.Actions.Select(action => action.Type), Is.EqualTo(new[]
        {
            MaintenanceActionType.Assigned, MaintenanceActionType.Started,
            MaintenanceActionType.Intervention, MaintenanceActionType.Resolved
        }));
        Assert.That(store.Actions.Last().RecordedByUserId, Is.EqualTo(actor.UserId));
        Assert.That(store.Saves, Is.EqualTo(4));
    }

    [Test]
    public async Task UnassignedWorkerCannotReadOrChangeAnotherTask()
    {
        var store = new Store();
        var service = Service(store);
        await service.AssignAsync(store.Request.Id, store.Workers[0].Id, Supervisor, default);
        var stranger = new MaintenanceActor(store.Workers[1].UserId, false);

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await service.GetTaskAsync(store.Request.Id, stranger, default));
        Assert.ThrowsAsync<MaintenanceAccessException>(async () => await service.StartAsync(store.Request.Id, stranger, default));
        Assert.ThrowsAsync<MaintenanceAccessException>(async () =>
            await service.ResolveAsync(store.Request.Id, "Fixed", stranger, default));
        Assert.ThrowsAsync<MaintenanceAccessException>(async () =>
            await service.AssignAsync(store.Request.Id, store.Workers[1].Id, stranger, default));
        Assert.That(await service.GetMyTasksAsync(stranger, null, 1, 50, default), Is.Empty);
        Assert.That(store.Actions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SupervisorCanProcessAnotherWorkersTaskAndIsRecordedAsActor()
    {
        var store = new Store();
        var service = Service(store);
        await service.AssignAsync(store.Request.Id, store.Workers[0].Id, Supervisor, default);
        await service.StartAsync(store.Request.Id, Supervisor, default);
        await service.ResolveAsync(store.Request.Id, "Supervisor repaired", Supervisor, default);

        Assert.That(store.Actions.Last().WorkerId, Is.EqualTo(store.Workers[0].Id));
        Assert.That(store.Actions.Last().RecordedByUserId, Is.EqualTo(Supervisor.UserId));
    }

    [Test]
    public async Task ReassignedWorkerLosesAccessButHistoryRemains()
    {
        var store = new Store();
        var service = Service(store);
        var firstActor = new MaintenanceActor(store.Workers[0].UserId, false);
        await service.AssignAsync(store.Request.Id, store.Workers[0].Id, Supervisor, default);
        await service.StartAsync(store.Request.Id, firstActor, default);
        await service.InterveneAsync(store.Request.Id, "Inspection", firstActor, default);
        await service.AssignAsync(store.Request.Id, store.Workers[1].Id, Supervisor, default);

        Assert.ThrowsAsync<MaintenanceAccessException>(async () =>
            await service.InterveneAsync(store.Request.Id, "Old worker", firstActor, default));
        Assert.That(await service.GetMyTasksAsync(firstActor, null, 1, 50, default), Is.Empty);
        Assert.That(await service.GetActionsAsync(store.Request.Id, Supervisor, 1, 50, default), Has.Count.EqualTo(4));
    }

    [Test]
    public async Task WorkerWithOpenTaskCannotBeDeactivated()
    {
        var store = new Store();
        var service = Service(store);
        var worker = store.Workers[0];
        await service.AssignAsync(store.Request.Id, worker.Id, Supervisor, default);
        var update = new UpdateWorkerRequest { Name = worker.Name, Specialization = worker.Specialization, IsActive = false };

        Assert.ThrowsAsync<MaintenanceConflictException>(async () =>
            await service.UpdateWorkerAsync(worker.Id, update, default));
        await service.AssignAsync(store.Request.Id, store.Workers[1].Id, Supervisor, default);
        var changed = await service.UpdateWorkerAsync(worker.Id, update, default);
        Assert.That(changed.IsActive, Is.False);
    }

    [Test]
    public async Task InactiveWorkerCannotChangeRequestEvenIfPreviouslyAssigned()
    {
        var store = new Store();
        var service = Service(store);
        var worker = store.Workers[0];
        await service.AssignAsync(store.Request.Id, worker.Id, Supervisor, default);
        worker.Update(worker.Name, worker.Specialization, false);
        Assert.ThrowsAsync<MaintenanceAccessException>(async () =>
            await service.StartAsync(store.Request.Id, new MaintenanceActor(worker.UserId, false), default));
    }

    [Test]
    public async Task StudentSeesOnlyOwnInterventionHistory()
    {
        var store = new Store();
        var service = Service(store);
        await service.AssignAsync(store.Request.Id, store.Workers[0].Id, Supervisor, default);
        Assert.That(await service.GetMyStudentActionsAsync(store.Request.Id, 1, 50, default), Has.Count.EqualTo(1));
        var other = new MaintenanceWorkService(store, new Directory(), new Student(Guid.NewGuid()), new Clock());
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await other.GetMyStudentActionsAsync(store.Request.Id, 1, 50, default));
    }

    [Test]
    public async Task WorkerRegistrationRequiresVerifiedAccountAndRejectsDuplicates()
    {
        var store = new Store();
        var directory = new Directory();
        var service = new MaintenanceWorkService(store, directory, new Student(store.Request.StudentId), new Clock());
        var request = new CreateWorkerRequest { UserId = Guid.NewGuid(), Name = "New worker", Specialization = "Plumbing" };
        var result = await service.CreateWorkerAsync(request, default);

        Assert.That(directory.Checked, Is.EqualTo(request.UserId));
        Assert.That(result.UserId, Is.EqualTo(request.UserId));
        Assert.ThrowsAsync<MaintenanceConflictException>(async () => await service.CreateWorkerAsync(request, default));
    }

    [Test]
    public void MissingStaffAccountDoesNotCreateWorker()
    {
        var store = new Store();
        var service = new MaintenanceWorkService(store, new Directory { Fail = true },
            new Student(store.Request.StudentId), new Clock());
        Assert.ThrowsAsync<ServiceLookupException>(async () => await service.CreateWorkerAsync(
            new CreateWorkerRequest { UserId = Guid.NewGuid(), Name = "Worker", Specialization = "Heating" }, default));
        Assert.That(store.Workers, Has.Count.EqualTo(2));
        Assert.That(store.Saves, Is.Zero);
    }

    [TestCase(0, 50)]
    [TestCase(1, 101)]
    [TestCase(int.MaxValue, 100)]
    public void InvalidPaginationIsRejected(int page, int size)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service(new Store()).GetWorkersAsync(page, size, default));
    }

    private static MaintenanceWorker Worker() => new(Guid.NewGuid(), "Worker", "Plumbing");

    private static MaintenanceRequest NewRequest()
    {
        return new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new MaintenanceCategory("Plumbing", null), "Leak", "Pipe leak", RequestPriority.High, Now);
    }

    private static MaintenanceRequest Accepted()
    {
        var request = NewRequest();
        request.Accept(Supervisor.UserId, Now);
        return request;
    }

    private static MaintenanceWorkService Service(Store store)
    {
        return new MaintenanceWorkService(store, new Directory(), new Student(store.Request.StudentId), new Clock());
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Directory : IStaffDirectoryClient
    {
        public Guid Checked { get; private set; }
        public bool Fail { get; init; }

        public Task EnsureActiveStaffAsync(Guid id, CancellationToken ct)
        {
            if (Fail) throw new ServiceLookupException(404, "Missing staff.");
            Checked = id;
            return Task.CompletedTask;
        }
    }

    private sealed class Student(Guid id) : ICurrentStudentContext
    {
        public Task<Guid> GetStudentIdAsync(CancellationToken ct) => Task.FromResult(id);
        public Task<CurrentAccommodation> GetActiveAccommodationAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Store : IWorkRepository
    {
        public MaintenanceRequest Request { get; } = Accepted();
        public List<MaintenanceWorker> Workers { get; } = [Worker(), Worker()];
        public List<MaintenanceAction> Actions { get; } = [];
        public int Saves { get; private set; }

        public Task<MaintenanceWorker?> GetWorkerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Workers.SingleOrDefault(worker => worker.Id == id));
        public Task<MaintenanceWorker?> GetWorkerByUserIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Workers.SingleOrDefault(worker => worker.UserId == id));
        public Task<IReadOnlyCollection<MaintenanceWorker>> GetWorkersAsync(int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<MaintenanceWorker>>(Workers.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
        public Task<bool> HasOpenAssignmentsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Request.AssignedWorkerId == id && Request.Status is RequestStatus.Assigned or RequestStatus.InProgress);
        public Task AddWorkerAsync(MaintenanceWorker worker, CancellationToken ct)
        {
            Workers.Add(worker);
            return Task.CompletedTask;
        }
        public Task<MaintenanceRequest?> GetRequestAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == Request.Id ? Request : null);
        public Task<IReadOnlyCollection<MaintenanceRequest>> GetAssignedRequestsAsync(
            Guid id, RequestStatus? status, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<MaintenanceRequest>>(new[] { Request }.Where(request =>
                request.AssignedWorkerId == id && (!status.HasValue || request.Status == status))
                .Skip((page - 1) * pageSize).Take(pageSize).ToArray());
        public Task<IReadOnlyCollection<MaintenanceAction>> GetActionsAsync(Guid id, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<MaintenanceAction>>(Actions.Where(action => action.RequestId == id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToArray());
        public Task AddActionAsync(MaintenanceAction action, CancellationToken ct)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
        public Task SaveAsync(CancellationToken ct)
        {
            Saves++;
            return Task.CompletedTask;
        }
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
}
