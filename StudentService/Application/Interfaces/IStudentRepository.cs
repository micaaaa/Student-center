using StudentCenter.StudentService.Domain.Entities;

namespace StudentCenter.StudentService.Application.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> StudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken = default);

    Task AddAsync(Student student, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
