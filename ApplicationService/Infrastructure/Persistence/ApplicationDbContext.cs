using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<StudentApplication> Applications => Set<StudentApplication>();
    public DbSet<ApplicationDocument> Documents => Set<ApplicationDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var document = modelBuilder.Entity<ApplicationDocument>();
        document.Property(x => x.FileName).HasMaxLength(255);
        document.Property(x => x.FileReference).HasMaxLength(32);
        document.Property(x => x.ContentType).HasMaxLength(100);
        document.HasIndex(x => x.FileReference).IsUnique();
        document.HasOne<StudentApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
