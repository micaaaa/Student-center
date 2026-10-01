using Microsoft.EntityFrameworkCore;
using StudentCenter.MaintenanceService.Domain.Entities;

namespace StudentCenter.MaintenanceService.Infrastructure.Persistence;

public sealed class MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options) : DbContext(options)
{
    public DbSet<MaintenanceCategory> Categories => Set<MaintenanceCategory>();
    public DbSet<MaintenanceRequest> Requests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceWorker> Workers => Set<MaintenanceWorker>();
    public DbSet<MaintenanceAction> Actions => Set<MaintenanceAction>();

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
        requests.Property(request => request.ResolutionDescription).HasMaxLength(4000);
        requests.Property(request => request.RowVersion).IsRowVersion();
        requests.HasIndex(request => new { request.StudentId, request.CreatedAtUtc });
        requests.HasIndex(request => new { request.Status, request.CreatedAtUtc });
        requests.HasOne<MaintenanceCategory>().WithMany().HasForeignKey(request => request.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        requests.HasOne<MaintenanceWorker>().WithMany().HasForeignKey(request => request.AssignedWorkerId)
            .OnDelete(DeleteBehavior.Restrict);
        requests.HasIndex(request => new { request.AssignedWorkerId, request.Status });

        var workers = modelBuilder.Entity<MaintenanceWorker>();
        workers.HasKey(worker => worker.Id);
        workers.HasIndex(worker => worker.UserId).IsUnique();
        workers.Property(worker => worker.Name).HasMaxLength(200).IsRequired();
        workers.Property(worker => worker.Specialization).HasMaxLength(200).IsRequired();
        workers.Property(worker => worker.RowVersion).IsRowVersion();

        var actions = modelBuilder.Entity<MaintenanceAction>();
        actions.HasKey(action => action.Id);
        actions.Property(action => action.Sequence).UseIdentityColumn();
        actions.HasIndex(action => action.Sequence).IsUnique();
        actions.HasIndex(action => new { action.RequestId, action.Sequence });
        actions.Property(action => action.Type).HasConversion<string>().HasMaxLength(20);
        actions.Property(action => action.Description).HasMaxLength(4000).IsRequired();
        actions.HasOne<MaintenanceRequest>().WithMany().HasForeignKey(action => action.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
        actions.HasOne<MaintenanceWorker>().WithMany().HasForeignKey(action => action.WorkerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
