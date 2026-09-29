using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Domain.Entities;
using StudentCenter.AccommodationService.Domain.Enums;
using StudentCenter.AccommodationService.Domain.Exceptions;

namespace StudentCenter.AccommodationService.Application.Services;

public sealed class AssignmentService(
    IAssignmentRepository assignments, IInventoryRepository inventory, TimeProvider clock)
{
    public Task<bool> ReceiveEligibilityAsync(EligibilityGrantedEvent message, CancellationToken ct) =>
        inventory.InTransactionAsync(async token =>
        {
            var incoming = new ReceivedEligibility(
                message.EventId, message.EligibilityId, message.StudentId,
                message.CompetitionId, message.AcademicYear, message.OccurredAtUtc);
            var existing = await assignments.GetByEventAsync(message.EventId, token)
                ?? await assignments.GetEligibilityAsync(message.EligibilityId, token);
            if (existing is not null)
            {
                if (!existing.HasSameDecision(incoming))
                    throw new ArgumentException("An eligibility identifier was reused with different decision data.");
                return false;
            }

            await assignments.AddEligibilityAsync(incoming, token);
            await inventory.SaveAsync(token);
            return true;
        }, ct);

    public async Task<IReadOnlyCollection<ReceivedEligibilityResponse>> GetEligibilitiesAsync(
        Guid competitionId, CancellationToken ct) =>
        (await assignments.GetEligibilitiesAsync(competitionId, ct))
            .Select(item => new ReceivedEligibilityResponse(
                item.Id, item.StudentId, item.CompetitionId, item.AcademicYear, item.GrantedAtUtc))
            .ToArray();

    public Task<AssignmentResponse> AssignAsync(AssignRoomRequest request, Guid staffId, CancellationToken ct) =>
        inventory.InTransactionAsync(async token =>
        {
            if (request.EligibilityId == Guid.Empty || request.RoomId == Guid.Empty || staffId == Guid.Empty)
                throw new ArgumentException("Eligibility, room and staff identifiers are required.");
            var eligibility = await assignments.GetEligibilityAsync(request.EligibilityId, token)
                ?? throw new KeyNotFoundException("Received accommodation eligibility was not found.");
            if (await assignments.HasActiveAssignmentAsync(eligibility.StudentId, token))
                throw new AccommodationConflictException("The student already has an active accommodation.");

            var room = await inventory.GetRoomAsync(request.RoomId, token)
                ?? throw new KeyNotFoundException("Room was not found.");
            var dorm = await inventory.GetDormAsync(room.DormId, token)
                ?? throw new KeyNotFoundException("Dorm was not found.");
            if (dorm.Status != DormStatus.Active)
                throw new AccommodationConflictException("An inactive dorm cannot receive assignments.");

            var assignment = new StudentAccommodation(
                eligibility, room.Id, staffId, clock.GetUtcNow().UtcDateTime);
            room.ReserveBed();
            await assignments.AddAssignmentAsync(assignment, token);
            await assignments.AddEventAsync(AccommodationOutboxMessage.From(assignment), token);
            await inventory.SaveAsync(token);
            return Map(assignment);
        }, ct);

    public Task<AssignmentResponse> CancelAsync(
        Guid id, string reason, Guid staffId, CancellationToken ct) =>
        inventory.InTransactionAsync(async token =>
        {
            var assignment = await FindAsync(id, token);
            var room = await inventory.GetRoomAsync(assignment.RoomId, token)
                ?? throw new KeyNotFoundException("Room was not found.");
            assignment.Cancel(staffId, reason, clock.GetUtcNow().UtcDateTime);
            room.ReleaseBed();
            await assignments.AddEventAsync(AccommodationOutboxMessage.From(assignment), token);
            await inventory.SaveAsync(token);
            return Map(assignment);
        }, ct);

    public Task<AssignmentResponse> MoveInAsync(
        Guid id, MoveInRequest request, Guid staffId, CancellationToken ct) =>
        inventory.InTransactionAsync(async token =>
        {
            var assignment = await FindAsync(id, token);
            assignment.RecordMoveIn(staffId, request.MedicalCertificateReference, clock.GetUtcNow().UtcDateTime);
            // The bed was already reserved when the room was assigned.
            await assignments.AddEventAsync(AccommodationOutboxMessage.From(assignment), token);
            await inventory.SaveAsync(token);
            return Map(assignment);
        }, ct);

    public Task<AssignmentResponse> MoveOutAsync(
        Guid id, MoveOutRequest request, Guid staffId, CancellationToken ct) =>
        inventory.InTransactionAsync(async token =>
        {
            var assignment = await FindAsync(id, token);
            var room = await inventory.GetRoomAsync(assignment.RoomId, token)
                ?? throw new KeyNotFoundException("Room was not found.");
            assignment.RecordMoveOut(staffId, request.Reason, clock.GetUtcNow().UtcDateTime);
            room.ReleaseBed();
            await assignments.AddEventAsync(AccommodationOutboxMessage.From(assignment), token);
            await inventory.SaveAsync(token);
            return Map(assignment);
        }, ct);

    public async Task<AssignmentResponse> GetAsync(Guid id, CancellationToken ct) =>
        Map(await FindAsync(id, ct));

    public async Task<IReadOnlyCollection<AssignmentResponse>> GetHistoryAsync(Guid studentId, CancellationToken ct) =>
        (await assignments.GetHistoryAsync(studentId, ct)).Select(Map).ToArray();

    private async Task<StudentAccommodation> FindAsync(Guid id, CancellationToken ct) =>
        await assignments.GetAssignmentAsync(id, ct)
            ?? throw new KeyNotFoundException("Accommodation assignment was not found.");

    private static AssignmentResponse Map(StudentAccommodation item) => new(
        item.Id, item.EligibilityId, item.StudentId, item.RoomId, item.AcademicYear,
        item.Status, item.AssignedBy, item.AssignedAtUtc,
        item.CancelledBy, item.CancelledAtUtc, item.CancellationReason,
        item.MoveIn is null ? null : new MoveInResponse(
            item.MoveIn.Id, item.MoveIn.DateUtc, item.MoveIn.MedicalCertificateReference, item.MoveIn.RecordedBy),
        item.MoveOut is null ? null : new MoveOutResponse(
            item.MoveOut.Id, item.MoveOut.DateUtc, item.MoveOut.Reason, item.MoveOut.RecordedBy));
}
