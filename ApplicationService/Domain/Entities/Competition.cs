using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class Competition
{
    private Competition()
    {
    }

    public Competition(string academicYear, string name, string? description, DateTime startDateUtc, DateTime endDateUtc)
    {
        Id = Guid.NewGuid();
        AcademicYear = academicYear;
        Name = name;
        Description = description;
        ApplicationStartDateUtc = startDateUtc;
        ApplicationEndDateUtc = endDateUtc;
        Status = CompetitionStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string AcademicYear { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime ApplicationStartDateUtc { get; private set; }
    public DateTime ApplicationEndDateUtc { get; private set; }
    public CompetitionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? AvailablePlaces { get; private set; }
    public DateTime? AppealDeadlineUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void ConfigureConclusion(int availablePlaces, DateTime appealDeadlineUtc, DateTime now)
    {
        if (Status != CompetitionStatus.Closed)
            throw new ApplicationConflictException("Only a closed competition can be configured for conclusion.");
        if (availablePlaces < 0 || appealDeadlineUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Places must be non-negative and the appeal deadline must use UTC.");
        if (AppealDeadlineUtc is null && appealDeadlineUtc <= now)
            throw new ArgumentException("The initial appeal deadline must be in the future.");
        if (AppealDeadlineUtc.HasValue && appealDeadlineUtc < AppealDeadlineUtc.Value)
            throw new ApplicationConflictException("An announced appeal deadline cannot be shortened.");

        AvailablePlaces = availablePlaces;
        AppealDeadlineUtc = appealDeadlineUtc;
    }

    public void FinalizeCompetition(DateTime now)
    {
        if (Status != CompetitionStatus.Closed || AvailablePlaces is null
            || AppealDeadlineUtc is null || now <= AppealDeadlineUtc)
            throw new ApplicationConflictException("The competition cannot be finalized before the appeal deadline.");

        Status = CompetitionStatus.Finalized;
    }

    public void Update(string name, string? description, DateTime startDateUtc, DateTime endDateUtc)
    {
        if (Status != CompetitionStatus.Draft)
            throw new InvalidOperationException("Only a draft competition can be edited.");
        Name = name;
        Description = description;
        ApplicationStartDateUtc = startDateUtc;
        ApplicationEndDateUtc = endDateUtc;
    }

    public void Open()
    {
        if (Status != CompetitionStatus.Draft)
            throw new InvalidOperationException("Only a draft competition can be opened.");
        Status = CompetitionStatus.Open;
    }

    public void Close()
    {
        if (Status != CompetitionStatus.Open)
            throw new InvalidOperationException("Only an open competition can be closed.");
        Status = CompetitionStatus.Closed;
    }
}
