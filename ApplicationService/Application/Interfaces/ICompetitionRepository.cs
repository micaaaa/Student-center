using StudentCenter.ApplicationService.Domain.Entities;
namespace StudentCenter.ApplicationService.Application.Interfaces;
public interface ICompetitionRepository { Task<Competition?> GetByIdAsync(Guid id,CancellationToken ct=default); Task<IReadOnlyCollection<Competition>> GetAllAsync(CancellationToken ct=default); Task AddAsync(Competition competition,CancellationToken ct=default); Task SaveChangesAsync(CancellationToken ct=default); }
