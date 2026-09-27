using Microsoft.EntityFrameworkCore;
using StudentCenter.StudentService.Domain.Entities;

namespace StudentCenter.StudentService.Infrastructure.Persistence;

public sealed class StudentDbContext(DbContextOptions<StudentDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
