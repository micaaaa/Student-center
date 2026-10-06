using StudentCenter.StudentService.Domain.Entities;

namespace StudentCenter.StudentService.Application.Interfaces;

public interface IStudentRepository
{
    Task<(IReadOnlyList<Student> Items, int TotalCount)> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> StudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken = default);

    Task AddAsync(Student student, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
