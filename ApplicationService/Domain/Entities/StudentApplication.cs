using StudentCenter.ApplicationService.Domain.Enums;
namespace StudentCenter.ApplicationService.Domain.Entities;
public sealed class StudentApplication
{
 private StudentApplication() { }
 public StudentApplication(Guid competitionId,Guid studentId){Id=Guid.NewGuid();CompetitionId=competitionId;StudentId=studentId;Status=ApplicationStatus.Draft;CreatedAtUtc=DateTime.UtcNow;}
 public Guid Id{get;private set;} public Guid CompetitionId{get;private set;} public Guid StudentId{get;private set;} public ApplicationStatus Status{get;private set;} public DateTime CreatedAtUtc{get;private set;} public DateTime? SubmittedAtUtc{get;private set;}
 public void Submit(){if(Status!=ApplicationStatus.Draft)throw new InvalidOperationException("Only a draft application can be submitted.");Status=ApplicationStatus.Submitted;SubmittedAtUtc=DateTime.UtcNow;}
}
