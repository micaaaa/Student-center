namespace StudentCenter.AccommodationService.Domain.Entities;

public sealed class ReceivedEligibility
{
    private ReceivedEligibility()
    {
    }

    public ReceivedEligibility(
        Guid eventId, Guid eligibilityId, Guid studentId, Guid competitionId,
        string academicYear, DateTime grantedAtUtc)
    {
        if (eventId == Guid.Empty || eligibilityId == Guid.Empty
            || studentId == Guid.Empty || competitionId == Guid.Empty)
            throw new ArgumentException("Eligibility event identifiers are required.");
        if (string.IsNullOrWhiteSpace(academicYear) || academicYear.Length > 20)
            throw new ArgumentException("Academic year is required and cannot exceed 20 characters.");
        if (grantedAtUtc == default || grantedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Eligibility decision time must be in UTC.");

        EventId = eventId;
        Id = eligibilityId;
        StudentId = studentId;
        CompetitionId = competitionId;
        AcademicYear = academicYear;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid CompetitionId { get; private set; }
    public string AcademicYear { get; private set; } = null!;
    public DateTime GrantedAtUtc { get; private set; }

    public bool HasSameDecision(ReceivedEligibility other) =>
        Id == other.Id && StudentId == other.StudentId && CompetitionId == other.CompetitionId
        && AcademicYear == other.AcademicYear && GrantedAtUtc == other.GrantedAtUtc;
}
