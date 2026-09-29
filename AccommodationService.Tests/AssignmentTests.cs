using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Application.Services;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Domain.Enums;
using StudentCenter.AccommodationService.Domain.Exceptions;
using StudentCenter.AccommodationService.Infrastructure.Persistence;

namespace StudentCenter.AccommodationService.Tests;

public sealed class AssignmentTests
{
    private Store store = null!;
    private AssignmentService service = null!;
    private readonly Guid staffId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        store = new Store();
        service = new AssignmentService(store, store, TimeProvider.System);
    }

    private static EligibilityGrantedEvent Event(Guid? studentId = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), studentId ?? Guid.NewGuid(), Guid.NewGuid(),
        "2026/2027", DateTime.UtcNow.AddDays(-1));

    [Test]
    public async Task PublishedApplicationEventCanBeConsumedWithoutChangingItsContract()
    {
        var decision = new StudentCenter.ApplicationService.Domain.Entities.AccommodationEligibility(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2026/2027", true, DateTime.UtcNow);
        var outbox = StudentCenter.ApplicationService.Domain.Entities.OutboxMessage.EligibilityGranted(decision);
        var message = System.Text.Json.JsonSerializer.Deserialize<EligibilityGrantedEvent>(outbox.Payload)!;

        Assert.That(message.EventId, Is.EqualTo(outbox.Id));
        Assert.That(await service.ReceiveEligibilityAsync(message, default), Is.True);
        Assert.That(store.Eligibilities.Single().StudentId, Is.EqualTo(decision.StudentId));
        Assert.That(store.Eligibilities.Single().Id, Is.EqualTo(decision.Id));
    }

    [Test]
    public async Task RedeliveryAndRepeatedDecisionDoNotCreateDuplicates()
    {
        var message = Event();
        Assert.That(await service.ReceiveEligibilityAsync(message, default), Is.True);
        Assert.That(await service.ReceiveEligibilityAsync(message, default), Is.False);
        Assert.That(await service.ReceiveEligibilityAsync(message with { EventId = Guid.NewGuid() }, default), Is.False);
        Assert.That(store.Eligibilities, Has.Count.EqualTo(1));
        Assert.That(await service.GetEligibilitiesAsync(message.CompetitionId, default), Has.Count.EqualTo(1));
        Assert.That(await service.GetEligibilitiesAsync(Guid.NewGuid(), default), Is.Empty);
    }

    [Test]
    public async Task ReusedEventOrDecisionWithDifferentDataIsRejected()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        Assert.ThrowsAsync<ArgumentException>(() => service.ReceiveEligibilityAsync(
            message with { StudentId = Guid.NewGuid() }, default));
        Assert.ThrowsAsync<ArgumentException>(() => service.ReceiveEligibilityAsync(
            message with { EligibilityId = Guid.NewGuid() }, default));
        Assert.ThrowsAsync<ArgumentException>(() => service.ReceiveEligibilityAsync(
            message with { EventId = Guid.NewGuid(), AcademicYear = "2027/2028" }, default));
        Assert.That(store.Eligibilities.Single().StudentId, Is.EqualTo(message.StudentId));
    }

    [Test]
    public void InvalidIncomingDecisionIsRejected()
    {
        var message = Event();
        Assert.ThrowsAsync<ArgumentException>(() => service.ReceiveEligibilityAsync(
            message with { EventId = Guid.Empty }, default));
        Assert.ThrowsAsync<ArgumentException>(() => service.ReceiveEligibilityAsync(
            message with { AcademicYear = "" }, default));
        Assert.ThrowsAsync<ArgumentException>(() => service.ReceiveEligibilityAsync(
            message with { OccurredAtUtc = default }, default));
        Assert.That(store.Eligibilities, Is.Empty);
    }

    [Test]
    public async Task AssignmentUsesReceivedStudentAndYearAndReservesBed()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var result = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        Assert.Multiple(() =>
        {
            Assert.That(result.StudentId, Is.EqualTo(message.StudentId));
            Assert.That(result.AcademicYear, Is.EqualTo(message.AcademicYear));
            Assert.That(result.AssignedBy, Is.EqualTo(staffId));
            Assert.That(result.Status, Is.EqualTo("ASSIGNED"));
            Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
            Assert.That(store.Room.Status, Is.EqualTo(RoomStatus.Full));
        });
        Assert.That((await service.GetAsync(result.Id, default)).Id, Is.EqualTo(result.Id));
    }

    [Test]
    public void AssignmentWithoutReceivedEligibilityIsRejected()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AssignAsync(new(Guid.NewGuid(), store.Room.Id), staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.Zero);
        Assert.That(store.Assignments, Is.Empty);
    }

    [Test]
    public async Task StudentCannotReceiveSecondActiveAssignmentEvenFromAnotherCompetition()
    {
        var message = Event();
        var other = Event(message.StudentId);
        await service.ReceiveEligibilityAsync(message, default);
        await service.ReceiveEligibilityAsync(other, default);
        await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.AssignAsync(new(other.EligibilityId, store.Room.Id), staffId, default));
        Assert.That(store.Assignments, Has.Count.EqualTo(1));
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
    }

    [Test]
    public async Task AnotherStudentCannotExceedRoomCapacity()
    {
        var first = Event();
        var second = Event();
        await service.ReceiveEligibilityAsync(first, default);
        await service.ReceiveEligibilityAsync(second, default);
        await service.AssignAsync(new(first.EligibilityId, store.Room.Id), staffId, default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.AssignAsync(new(second.EligibilityId, store.Room.Id), staffId, default));
        Assert.That(store.Assignments, Has.Count.EqualTo(1));
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
    }

    [TestCase(RoomStatus.Inactive)]
    [TestCase(RoomStatus.Maintenance)]
    public async Task UnavailableRoomCannotReceiveAssignment(RoomStatus status)
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        store.Room.Update("101", 1, 1, status);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default));
        Assert.That(store.Assignments, Is.Empty);
    }

    [Test]
    public async Task AvailableRoomInInactiveDormCannotReceiveAssignment()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        store.Dorm.Update("Dom", "Adresa", "Grad", "I", 10, DormStatus.Inactive);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.Zero);
    }

    [Test]
    public async Task CancellationReleasesBedKeepsHistoryAndAllowsNewAssignment()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var first = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        var cancelled = await service.CancelAsync(first.Id, "  Promena sobe  ", staffId, default);
        Assert.That(cancelled.Status, Is.EqualTo("CANCELLED"));
        Assert.That(cancelled.CancellationReason, Is.EqualTo("Promena sobe"));
        Assert.That(cancelled.CancelledBy, Is.EqualTo(staffId));
        Assert.That(store.Room.OccupiedBeds, Is.Zero);
        Assert.That(store.Room.Status, Is.EqualTo(RoomStatus.Available));
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.CancelAsync(first.Id, "Ponovo", staffId, default));
        await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        var history = await service.GetHistoryAsync(message.StudentId, default);
        Assert.That(history.Select(item => item.Status), Is.EquivalentTo(new[] { "CANCELLED", "ASSIGNED" }));
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
        Assert.That(await service.GetHistoryAsync(Guid.NewGuid(), default), Is.Empty);
    }

    [Test]
    public async Task InvalidCancellationDoesNotFreeBed()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var result = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        Assert.ThrowsAsync<ArgumentException>(() => service.CancelAsync(result.Id, " ", staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
        Assert.That(store.Assignments.Single().IsActive, Is.True);
    }

    [Test]
    public async Task MissingStaffOrRoomIsRejectedBeforeReservingBed()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        Assert.ThrowsAsync<ArgumentException>(() =>
            service.AssignAsync(new(message.EligibilityId, store.Room.Id), Guid.Empty, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AssignAsync(new(message.EligibilityId, Guid.NewGuid()), staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.Zero);
    }

    [Test]
    public void DatabaseEnforcesDeduplicationAndOneActiveAssignmentPerStudent()
    {
        using var db = new AccommodationDbContext(new DbContextOptionsBuilder<AccommodationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var eligibility = db.Model.FindEntityType(typeof(ReceivedEligibility))!;
        Assert.That(eligibility.GetIndexes().Any(index => index.IsUnique
            && index.Properties.Single().Name == nameof(ReceivedEligibility.EventId)), Is.True);
        var assignment = db.Model.FindEntityType(typeof(StudentAccommodation))!;
        var studentIndex = assignment.GetIndexes().Single(index => index.IsUnique);
        Assert.That(studentIndex.Properties.Single().Name, Is.EqualTo(nameof(StudentAccommodation.StudentId)));
        Assert.That(studentIndex.GetFilter(), Is.EqualTo("[IsActive] = 1"));
        Assert.That(assignment.FindProperty(nameof(StudentAccommodation.RowVersion))!.IsConcurrencyToken, Is.True);
    }

    private sealed class Store : IAssignmentRepository, IInventoryRepository
    {
        public Dorm Dorm { get; } = new("Dom", "Adresa", "Grad", "I", 10);
        public Room Room { get; }
        public List<ReceivedEligibility> Eligibilities { get; } = [];
        public List<StudentAccommodation> Assignments { get; } = [];

        public Store()
        {
            Room = new Room(Dorm.Id, "101", 1, 1);
        }

        public Task<ReceivedEligibility?> GetEligibilityAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Eligibilities.SingleOrDefault(item => item.Id == id));

        public Task<ReceivedEligibility?> GetByEventAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult(Eligibilities.SingleOrDefault(item => item.EventId == eventId));

        public Task<IReadOnlyCollection<ReceivedEligibility>> GetEligibilitiesAsync(Guid competitionId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<ReceivedEligibility>>(
                Eligibilities.Where(item => item.CompetitionId == competitionId).ToArray());

        public Task AddEligibilityAsync(ReceivedEligibility eligibility, CancellationToken ct)
        {
            Eligibilities.Add(eligibility);
            return Task.CompletedTask;
        }

        public Task<bool> HasActiveAssignmentAsync(Guid studentId, CancellationToken ct) =>
            Task.FromResult(Assignments.Any(item => item.StudentId == studentId && item.IsActive));

        public Task<StudentAccommodation?> GetAssignmentAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Assignments.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyCollection<StudentAccommodation>> GetHistoryAsync(Guid studentId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<StudentAccommodation>>(
                Assignments.Where(item => item.StudentId == studentId).ToArray());

        public Task AddAssignmentAsync(StudentAccommodation assignment, CancellationToken ct)
        {
            Assignments.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<Dorm?> GetDormAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == Dorm.Id ? Dorm : null);

        public Task<Room?> GetRoomAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == Room.Id ? Room : null);

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);

        public Task<IReadOnlyCollection<Dorm>> GetDormsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Room>> GetRoomsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> RoomNumberExistsAsync(Guid id, string number, Guid? exceptId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<long> GetRoomCapacityAsync(Guid id, Guid? exceptId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasOccupiedRoomsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task AddDormAsync(Dorm dorm, CancellationToken ct) => throw new NotSupportedException();
        public Task AddRoomAsync(Room room, CancellationToken ct) => throw new NotSupportedException();
    }
}
