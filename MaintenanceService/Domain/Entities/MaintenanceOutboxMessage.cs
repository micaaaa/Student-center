using System.Text.Json;
using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.Domain.Entities;

public sealed class MaintenanceOutboxMessage
{
    private MaintenanceOutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public Guid ActionId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public static MaintenanceOutboxMessage From(
        MaintenanceAction action, MaintenanceRequest request, MaintenanceWorker worker)
    {
        if (action.RequestId != request.Id || action.WorkerId != worker.Id)
        {
            throw new ArgumentException("The event must reference the action's request and worker.");
        }

        var type = action.Type switch
        {
            MaintenanceActionType.Assigned => "MaintenanceWorkerAssigned",
            MaintenanceActionType.Resolved => "MaintenanceRequestResolved",
            _ => throw new ArgumentException("Only assignment and resolution actions produce events.")
        };
        var id = Guid.NewGuid();
        var timestamp = DateTime.SpecifyKind(action.CreatedAtUtc, DateTimeKind.Utc);
        return new MaintenanceOutboxMessage
        {
            Id = id,
            ActionId = action.Id,
            Type = type,
            OccurredAtUtc = timestamp,
            Payload = JsonSerializer.Serialize(new MaintenanceNotificationEvent(
                id, action.Id, request.Id, request.StudentId, worker.Id, worker.UserId,
                action.RecordedByUserId, type, timestamp))
        };
    }

    public void MarkPublished(DateTime now)
    {
        PublishedAtUtc ??= now;
    }
}

// Consumers deduplicate by EventId. Text and contact details remain in their owning services.
public sealed record MaintenanceNotificationEvent(
    Guid EventId, Guid ActionId, Guid RequestId, Guid StudentId, Guid WorkerId,
    Guid WorkerUserId, Guid RecordedByUserId, string Type, DateTime OccurredAtUtc);
