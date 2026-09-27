using StudentCenter.ApplicationService.Domain.Enums;
namespace StudentCenter.ApplicationService.Domain.Entities;
public sealed class Competition
{
    private Competition() { }
    public Competition(string academicYear, string name, string? description, DateTime startDateUtc, DateTime endDateUtc)
    { Id=Guid.NewGuid(); AcademicYear=academicYear; Name=name; Description=description; ApplicationStartDateUtc=startDateUtc; ApplicationEndDateUtc=endDateUtc; Status=CompetitionStatus.Draft; CreatedAtUtc=DateTime.UtcNow; }
    public Guid Id { get; private set; } public string AcademicYear { get; private set; }=null!; public string Name { get; private set; }=null!; public string? Description { get; private set; } public DateTime ApplicationStartDateUtc { get; private set; } public DateTime ApplicationEndDateUtc { get; private set; } public CompetitionStatus Status { get; private set; } public DateTime CreatedAtUtc { get; private set; }
    public void Update(string name, string? description, DateTime startDateUtc, DateTime endDateUtc) { if(Status!=CompetitionStatus.Draft) throw new InvalidOperationException("Only a draft competition can be edited."); Name=name; Description=description; ApplicationStartDateUtc=startDateUtc; ApplicationEndDateUtc=endDateUtc; }
    public void Open() { if(Status!=CompetitionStatus.Draft) throw new InvalidOperationException("Only a draft competition can be opened."); Status=CompetitionStatus.Open; }
    public void Close() { if(Status!=CompetitionStatus.Open) throw new InvalidOperationException("Only an open competition can be closed."); Status=CompetitionStatus.Closed; }
}
