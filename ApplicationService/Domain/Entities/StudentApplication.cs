using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class StudentApplication
{
    private StudentApplication()
    {
    }

    public StudentApplication(Guid competitionId, Guid studentId, string? note = null)
    {
        Id = Guid.NewGuid();
        CompetitionId = competitionId;
        StudentId = studentId;
        Note = NormalizeNote(note);
        Status = ApplicationStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CompetitionId { get; private set; }
    public Guid StudentId { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public string? Note { get; private set; }

    public void UpdateNote(string? note)
    {
        if (Status != ApplicationStatus.Draft)
            throw new ApplicationConflictException("Only a draft application can be edited.");
        Note = NormalizeNote(note);
    }

    public void Submit()
    {
        if (Status != ApplicationStatus.Draft)
            throw new ApplicationConflictException("Only a draft application can be submitted.");
        Status = ApplicationStatus.Submitted;
        SubmittedAtUtc = DateTime.UtcNow;
    }

    public void StartReview()
    {
        if (Status != ApplicationStatus.Submitted)
            throw new ApplicationConflictException("Only a submitted application can enter review.");

        Status = ApplicationStatus.UnderReview;
    }

    public void Withdraw()
    {
        if (Status is not (ApplicationStatus.Draft or ApplicationStatus.Submitted))
            throw new ApplicationConflictException("Only a draft or submitted application can be withdrawn.");
        Status = ApplicationStatus.Withdrawn;
    }

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
