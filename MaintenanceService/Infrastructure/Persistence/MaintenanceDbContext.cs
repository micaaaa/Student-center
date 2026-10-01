using Microsoft.EntityFrameworkCore;
using StudentCenter.MaintenanceService.Domain.Entities;

namespace StudentCenter.MaintenanceService.Infrastructure.Persistence;

public sealed class MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options) : DbContext(options)
{
    public DbSet<MaintenanceCategory> Categories => Set<MaintenanceCategory>();
    public DbSet<MaintenanceRequest> Requests => Set<MaintenanceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var categories = modelBuilder.Entity<MaintenanceCategory>();
        categories.HasKey(category => category.Id);
        categories.Property(category => category.Name).HasMaxLength(100).IsRequired();
        categories.Property(category => category.Description).HasMaxLength(1000);
        categories.Property(category => category.RowVersion).IsRowVersion();
        categories.HasIndex(category => category.Name).IsUnique();

        var requests = modelBuilder.Entity<MaintenanceRequest>();
        requests.HasKey(request => request.Id);
        requests.Property(request => request.Title).HasMaxLength(200).IsRequired();
        requests.Property(request => request.Description).HasMaxLength(4000).IsRequired();
        requests.Property(request => request.Priority).HasConversion<string>().HasMaxLength(20);
        requests.Property(request => request.Status).HasConversion<string>().HasMaxLength(20);
        requests.Property(request => request.RejectionReason).HasMaxLength(2000);
        requests.Property(request => request.RowVersion).IsRowVersion();
        requests.HasIndex(request => new { request.StudentId, request.CreatedAtUtc });
        requests.HasIndex(request => new { request.Status, request.CreatedAtUtc });
        requests.HasOne<MaintenanceCategory>().WithMany().HasForeignKey(request => request.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
