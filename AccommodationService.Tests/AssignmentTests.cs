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

    [Test]
    public async Task MoveInKeepsReservedBedAndMoveOutReleasesItWhilePreservingHistory()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var assigned = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        var movedIn = await service.MoveInAsync(assigned.Id,
            new MoveInRequest { MedicalCertificateReference = "  CERT-2026-1  " }, staffId, default);

        Assert.That(movedIn.Status, Is.EqualTo("ACTIVE"));
        Assert.That(movedIn.MoveIn!.MedicalCertificateReference, Is.EqualTo("CERT-2026-1"));
        Assert.That(movedIn.MoveIn.RecordedBy, Is.EqualTo(staffId));
        Assert.That(movedIn.MoveIn.DateUtc, Is.GreaterThanOrEqualTo(assigned.AssignedAtUtc));
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
        Assert.That(store.Assignments.Single().IsActive, Is.True);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.CancelAsync(assigned.Id, "Otkazivanje", staffId, default));
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default));

        var movedOut = await service.MoveOutAsync(assigned.Id,
            new MoveOutRequest { Reason = "  Završetak boravka  " }, staffId, default);
        Assert.That(movedOut.Status, Is.EqualTo("COMPLETED"));
        Assert.That(movedOut.MoveOut!.Reason, Is.EqualTo("Završetak boravka"));
        Assert.That(movedOut.MoveOut.RecordedBy, Is.EqualTo(staffId));
        Assert.That(movedOut.MoveOut.DateUtc, Is.GreaterThanOrEqualTo(movedIn.MoveIn.DateUtc));
        Assert.That(store.Room.OccupiedBeds, Is.Zero);
        Assert.That(store.Room.Status, Is.EqualTo(RoomStatus.Available));
        Assert.That(store.Assignments.Single().IsActive, Is.False);

        await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        var history = await service.GetHistoryAsync(message.StudentId, default);
        Assert.That(history, Has.Count.EqualTo(2));
        var completed = history.Single(item => item.Id == assigned.Id);
        Assert.That(completed.MoveIn!.Id, Is.EqualTo(movedIn.MoveIn.Id));
        Assert.That(completed.MoveOut!.Id, Is.EqualTo(movedOut.MoveOut.Id));
    }

    [Test]
    public async Task RepeatedMoveInOrMoveOutDoesNotChangeOccupancy()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var assigned = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        var moveIn = new MoveInRequest { MedicalCertificateReference = "CERT-1" };
        var moveOut = new MoveOutRequest { Reason = "Završetak" };
        await service.MoveInAsync(assigned.Id, moveIn, staffId, default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.MoveInAsync(assigned.Id, moveIn, staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
        await service.MoveOutAsync(assigned.Id, moveOut, staffId, default);
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.MoveOutAsync(assigned.Id, moveOut, staffId, default));
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.MoveInAsync(assigned.Id, moveIn, staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.Zero);
    }

    [Test]
    public async Task MoveOutBeforeMoveInAndMoveInAfterCancellationAreRejected()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var assigned = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        Assert.ThrowsAsync<AccommodationConflictException>(() => service.MoveOutAsync(assigned.Id,
            new MoveOutRequest { Reason = "Završetak" }, staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
        await service.CancelAsync(assigned.Id, "Odustao", staffId, default);
        Assert.ThrowsAsync<AccommodationConflictException>(() => service.MoveInAsync(assigned.Id,
            new MoveInRequest { MedicalCertificateReference = "CERT-1" }, staffId, default));
        Assert.That(store.Room.OccupiedBeds, Is.Zero);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task EmptyCertificateAndMoveOutReasonDoNotChangeState(string value)
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var assigned = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        Assert.ThrowsAsync<ArgumentException>(() => service.MoveInAsync(assigned.Id,
            new MoveInRequest { MedicalCertificateReference = value }, staffId, default));
        Assert.That(store.Assignments.Single().Status, Is.EqualTo("ASSIGNED"));
        Assert.That(store.Assignments.Single().MoveIn, Is.Null);
        await service.MoveInAsync(assigned.Id,
            new MoveInRequest { MedicalCertificateReference = "CERT-1" }, staffId, default);
        Assert.ThrowsAsync<ArgumentException>(() => service.MoveOutAsync(assigned.Id,
            new MoveOutRequest { Reason = value }, staffId, default));
        Assert.That(store.Assignments.Single().Status, Is.EqualTo("ACTIVE"));
        Assert.That(store.Assignments.Single().MoveOut, Is.Null);
        Assert.That(store.Room.OccupiedBeds, Is.EqualTo(1));
    }

    [Test]
    public async Task MissingAccommodationReturnsNotFoundForBothTransitions()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.MoveInAsync(Guid.NewGuid(),
            new MoveInRequest { MedicalCertificateReference = "CERT-1" }, staffId, default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.MoveOutAsync(Guid.NewGuid(),
            new MoveOutRequest { Reason = "Završetak" }, staffId, default));
        Assert.That(await service.GetHistoryAsync(Guid.NewGuid(), default), Is.Empty);
    }

    [Test]
    public void InvalidStaffDatesAndOversizedFieldsAreRejectedBeforeMutation()
    {
        var now = DateTime.UtcNow;
        var eligibility = new ReceivedEligibility(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "2026/2027", now.AddDays(-1));
        var assignment = new StudentAccommodation(eligibility, store.Room.Id, staffId, now);
        Assert.Throws<ArgumentException>(() => assignment.RecordMoveIn(Guid.Empty, "CERT-1", now));
        Assert.Throws<ArgumentException>(() => assignment.RecordMoveIn(staffId, "CERT-1", now.AddSeconds(-1)));
        Assert.Throws<ArgumentException>(() => assignment.RecordMoveIn(staffId, new string('x', 251), now));
        Assert.That(assignment.MoveIn, Is.Null);
        assignment.RecordMoveIn(staffId, "CERT-1", now.AddHours(1));
        Assert.Throws<ArgumentException>(() => assignment.RecordMoveOut(Guid.Empty, "Kraj", now.AddHours(2)));
        Assert.Throws<ArgumentException>(() => assignment.RecordMoveOut(staffId, "Kraj", now));
        Assert.Throws<ArgumentException>(() => assignment.RecordMoveOut(staffId, new string('x', 1001), now.AddHours(2)));
        Assert.That(assignment.MoveOut, Is.Null);
        Assert.That(assignment.IsActive, Is.True);
    }

    [Test]
    public void EfTracksNewMoveRecordsAsInsertsAndEnforcesOneOfEachPerAccommodation()
    {
        using var db = new AccommodationDbContext(new DbContextOptionsBuilder<AccommodationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var now = DateTime.UtcNow;
        var eligibility = new ReceivedEligibility(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "2026/2027", now.AddDays(-1));
        var assignment = new StudentAccommodation(eligibility, store.Room.Id, staffId, now);
        db.Attach(assignment);
        assignment.RecordMoveIn(staffId, "CERT-1", now);
        db.ChangeTracker.DetectChanges();
        Assert.That(db.Entry(assignment.MoveIn!).State, Is.EqualTo(EntityState.Added));
        db.ChangeTracker.AcceptAllChanges();
        assignment.RecordMoveOut(staffId, "Kraj", now.AddHours(1));
        db.ChangeTracker.DetectChanges();
        Assert.That(db.Entry(assignment.MoveOut!).State, Is.EqualTo(EntityState.Added));
        foreach (var type in new[] { typeof(MoveIn), typeof(MoveOut) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.That(entity.GetIndexes().Single().IsUnique, Is.True);
            Assert.That(entity.GetForeignKeys().Single().DeleteBehavior, Is.EqualTo(DeleteBehavior.Restrict));
        }
    }

    [Test]
    public async Task SuccessfulLifecycleCreatesOneOutboxEventForEachTransition()
    {
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var assigned = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        await service.MoveInAsync(assigned.Id,
            new MoveInRequest { MedicalCertificateReference = "PRIVATE-CERT" }, staffId, default);
        await service.MoveOutAsync(assigned.Id,
            new MoveOutRequest { Reason = "PRIVATE-REASON" }, staffId, default);

        Assert.That(store.Events.Select(item => item.Type), Is.EqualTo(new[]
        {
            "AccommodationAssigned", "StudentMovedIn", "StudentMovedOut"
        }));
        foreach (var item in store.Events)
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<AccommodationLifecycleEvent>(item.Payload)!;
            Assert.That(payload.EventId, Is.EqualTo(item.Id));
            Assert.That(payload.AccommodationId, Is.EqualTo(assigned.Id));
            Assert.That(payload.StudentId, Is.EqualTo(message.StudentId));
            Assert.That(payload.RoomId, Is.EqualTo(store.Room.Id));
            Assert.That(payload.AcademicYear, Is.EqualTo(message.AcademicYear));
            Assert.That(payload.OccurredAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(item.PublishedAtUtc, Is.Null);
            Assert.That(item.Payload, Does.Not.Contain("PRIVATE"));
        }

        Assert.ThrowsAsync<AccommodationConflictException>(() => service.MoveOutAsync(assigned.Id,
            new MoveOutRequest { Reason = "Ponovo" }, staffId, default));
        Assert.That(store.Events, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task CancellationEmitsAnEventButRejectedActionsDoNot()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AssignAsync(new(Guid.NewGuid(), store.Room.Id), staffId, default));
        Assert.That(store.Events, Is.Empty);
        var message = Event();
        await service.ReceiveEligibilityAsync(message, default);
        var assigned = await service.AssignAsync(new(message.EligibilityId, store.Room.Id), staffId, default);
        Assert.ThrowsAsync<ArgumentException>(() => service.MoveInAsync(assigned.Id,
            new MoveInRequest { MedicalCertificateReference = "" }, staffId, default));
        Assert.That(store.Events, Has.Count.EqualTo(1));
        await service.CancelAsync(assigned.Id, "Odustao", staffId, default);
        Assert.That(store.Events.Last().Type, Is.EqualTo("AccommodationAssignmentCancelled"));
        Assert.ThrowsAsync<AccommodationConflictException>(() =>
            service.CancelAsync(assigned.Id, "Ponovo", staffId, default));
        Assert.That(store.Events, Has.Count.EqualTo(2));
    }

    private sealed class Store : IAssignmentRepository, IInventoryRepository
    {
        public Dorm Dorm { get; } = new("Dom", "Adresa", "Grad", "I", 10);
        public Room Room { get; }
        public List<ReceivedEligibility> Eligibilities { get; } = [];
        public List<StudentAccommodation> Assignments { get; } = [];
        public List<AccommodationOutboxMessage> Events { get; } = [];

        public Task AddEventAsync(AccommodationOutboxMessage message, CancellationToken ct)
        {
            Events.Add(message);
            return Task.CompletedTask;
        }

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
