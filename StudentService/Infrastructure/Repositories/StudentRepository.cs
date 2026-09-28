using Microsoft.EntityFrameworkCore;
using StudentCenter.StudentService.Application.Interfaces;
using StudentCenter.StudentService.Domain.Entities;
using StudentCenter.StudentService.Infrastructure.Persistence;

namespace StudentCenter.StudentService.Infrastructure.Repositories;

public sealed class StudentRepository(StudentDbContext dbContext) : IStudentRepository
{
    public Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Students.SingleOrDefaultAsync(student => student.Id == id, cancellationToken);

    public Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Students.SingleOrDefaultAsync(student => student.UserId == userId, cancellationToken);

    public Task<bool> StudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken = default) =>
        dbContext.Students.AnyAsync(student => student.StudentNumber == studentNumber, cancellationToken);

    public async Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        await dbContext.Students.AddAsync(student, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
