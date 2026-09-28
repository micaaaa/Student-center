using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<StudentApplication> Applications => Set<StudentApplication>();
    public DbSet<ApplicationDocument> Documents => Set<ApplicationDocument>();
    public DbSet<ScoringResult> Scores => Set<ScoringResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentApplication>().Property(x => x.Status).IsConcurrencyToken();
        var document = modelBuilder.Entity<ApplicationDocument>();
        document.Property(x => x.ReviewComment).HasMaxLength(2000);
        document.Property(x => x.ReviewedAtUtc).IsConcurrencyToken();
        document.Property(x => x.FileName).HasMaxLength(255);
        document.Property(x => x.FileReference).HasMaxLength(32);
        document.Property(x => x.ContentType).HasMaxLength(100);
        document.HasIndex(x => x.FileReference).IsUnique();
        document.HasOne<StudentApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);

        var score = modelBuilder.Entity<ScoringResult>();
        score.HasIndex(x => x.ApplicationId).IsUnique();
        score.HasOne<StudentApplication>().WithOne().HasForeignKey<ScoringResult>(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        score.Property(x => x.AcademicPoints).HasPrecision(18, 2);
        score.Property(x => x.IncomePoints).HasPrecision(18, 2);
        score.Property(x => x.ECTSPoints).HasPrecision(18, 2);
        score.Property(x => x.StudyYearPoints).HasPrecision(18, 2);
        score.Property(x => x.AdditionalPoints).HasPrecision(18, 2);
        score.Property(x => x.TotalPoints).HasPrecision(18, 2);
        score.Property(x => x.DocumentReviewFingerprint).HasMaxLength(64);
        score.Property(x => x.RowVersion).IsRowVersion();
    }
}
