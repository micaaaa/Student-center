using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class Appeal
{
    private Appeal()
    {
    }

    public Appeal(Guid applicationId, Guid competitionId, Guid studentId, string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 4000)
            throw new ArgumentException("Appeal reason must contain between 1 and 4000 characters.");

        Id = Guid.NewGuid();
        ApplicationId = applicationId;
        CompetitionId = competitionId;
        StudentId = studentId;
        Reason = reason.Trim();
        SubmittedAtUtc = now;
        Status = AppealStatus.Submitted;
    }

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid CompetitionId { get; private set; }
    public Guid StudentId { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTime SubmittedAtUtc { get; private set; }
    public AppealStatus Status { get; private set; }
    public string? Response { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void StartReview()
    {
        if (Status != AppealStatus.Submitted)
            throw new ApplicationConflictException("Only a submitted appeal can enter review.");
        Status = AppealStatus.UnderReview;
    }

    public void Resolve(bool accepted, string response, Guid userId, DateTime now)
    {
        if (Status != AppealStatus.UnderReview)
            throw new ApplicationConflictException("Only an appeal under review can be resolved.");
        if (string.IsNullOrWhiteSpace(response) || response.Trim().Length > 4000)
            throw new ArgumentException("An appeal response of at most 4000 characters is required.");
        if (userId == Guid.Empty)
            throw new ArgumentException("A resolving user is required.");

        Response = response.Trim();
        Status = accepted ? AppealStatus.Accepted : AppealStatus.Rejected;
        ResolvedAtUtc = now;
        ResolvedByUserId = userId;
    }
}
