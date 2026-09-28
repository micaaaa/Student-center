namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class AccommodationEligibility
{
    private AccommodationEligibility()
    {
    }

    public AccommodationEligibility(
        Guid competitionId, Guid applicationId, Guid studentId, Guid rankingId,
        string academicYear, bool eligible, DateTime now)
    {
        Id = Guid.NewGuid();
        CompetitionId = competitionId;
        ApplicationId = applicationId;
        StudentId = studentId;
        RankingId = rankingId;
        AcademicYear = academicYear;
        Eligible = eligible;
        DecisionDateUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid CompetitionId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid RankingId { get; private set; }
    public string AcademicYear { get; private set; } = null!;
    public bool Eligible { get; private set; }
    public DateTime DecisionDateUtc { get; private set; }
}
