using Microsoft.EntityFrameworkCore;
using StudentCenter.StudentService.Application.Interfaces;
using StudentCenter.StudentService.Domain.Entities;
using StudentCenter.StudentService.Infrastructure.Persistence;

namespace StudentCenter.StudentService.Infrastructure.Repositories;

public sealed class StudentRepository(StudentDbContext dbContext) : IStudentRepository
{
    public async Task<(IReadOnlyList<Student> Items, int TotalCount)> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Students.AsNoTracking();
        foreach (var term in (search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            query = query.Where(student =>
                student.FirstName.Contains(term) ||
                student.LastName.Contains(term) ||
                student.StudentNumber.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(student => student.LastName)
            .ThenBy(student => student.FirstName)
            .ThenBy(student => student.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }
    public Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Students.SingleOrDefaultAsync(student => student.Id == id, cancellationToken);

    public Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Students.SingleOrDefaultAsync(student => student.UserId == userId, cancellationToken);

    public Task<bool> StudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken = default) =>
        dbContext.Students.AnyAsync(student => student.StudentNumber == studentNumber, cancellationToken);

    public async Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        await dbContext.Students.AddAsync(student, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException
            { Number: 2601 or 2627 })
        {
            throw new StudentCenter.StudentService.Application.Exceptions.ConflictException(
                "A student profile with this account or student number already exists.");
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
