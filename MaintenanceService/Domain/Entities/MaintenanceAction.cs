using StudentCenter.MaintenanceService.Domain.Enums;

namespace StudentCenter.MaintenanceService.Domain.Entities;

public sealed class MaintenanceAction
{
    private MaintenanceAction()
    {
    }

    internal MaintenanceAction(Guid requestId, Guid workerId, Guid actorId,
        MaintenanceActionType type, string description, DateTimeOffset now)
    {
        if (requestId == Guid.Empty || workerId == Guid.Empty || actorId == Guid.Empty || !Enum.IsDefined(type))
        {
            throw new ArgumentException("Valid request, worker, actor and action type are required.");
        }

        Description = MaintenanceCategory.RequiredText(description, 4000, "Intervention description");
        Id = Guid.NewGuid();
        RequestId = requestId;
        WorkerId = workerId;
        RecordedByUserId = actorId;
        Type = type;
        CreatedAtUtc = now.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid WorkerId { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public MaintenanceActionType Type { get; private set; }
    public string Description { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
}
