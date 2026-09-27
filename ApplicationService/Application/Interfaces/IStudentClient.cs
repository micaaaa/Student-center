namespace StudentCenter.ApplicationService.Application.Interfaces;
public interface IStudentClient { Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct=default); }
