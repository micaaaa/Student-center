using StudentCenter.ApplicationService.Domain.Entities;
namespace StudentCenter.ApplicationService.Application.Interfaces;
public interface IApplicationRepository { Task<StudentApplication?> GetAsync(Guid id,CancellationToken ct=default); Task<StudentApplication?> GetForStudentAsync(Guid competitionId,Guid studentId,CancellationToken ct=default); Task<IReadOnlyCollection<StudentApplication>> GetMineAsync(Guid studentId,CancellationToken ct=default); Task AddAsync(StudentApplication application,CancellationToken ct=default); Task SaveAsync(CancellationToken ct=default); }
