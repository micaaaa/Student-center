using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentCenter.StudentService.Domain.Entities;

namespace StudentCenter.StudentService.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(student => student.Id);
        builder.Property(student => student.StudentNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(student => student.StudentNumber).IsUnique();
        builder.HasIndex(student => student.UserId).IsUnique();
        builder.Property(student => student.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(student => student.LastName).HasMaxLength(100).IsRequired();
        builder.Property(student => student.Email).HasMaxLength(256).IsRequired();
        builder.Property(student => student.Phone).HasMaxLength(30);
        builder.Property(student => student.Faculty).HasMaxLength(200);
        builder.Property(student => student.StudyProgram).HasMaxLength(200);
        builder.Property(student => student.StudyLevel).HasMaxLength(100);
        builder.Property(student => student.Address).HasMaxLength(500);
        builder.Property(student => student.FundingType).HasConversion<string>().HasMaxLength(30);
        builder.Property(student => student.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}
