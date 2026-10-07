using StudentCenter.ApplicationService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<StudentApplication> Applications => Set<StudentApplication>();
    public DbSet<ApplicationDocument> Documents => Set<ApplicationDocument>();
    public DbSet<ScoringResult> Scores => Set<ScoringResult>();
    public DbSet<Ranking> Rankings => Set<Ranking>();
    public DbSet<RankingEntry> RankingEntries => Set<RankingEntry>();
    public DbSet<Appeal> Appeals => Set<Appeal>();
    public DbSet<AccommodationEligibility> Eligibilities => Set<AccommodationEligibility>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Save notification events in the same transaction as the business state change.
        ChangeTracker.DetectChanges();
        var events = new List<OutboxMessage>();
        foreach (var entry in ChangeTracker.Entries<StudentApplication>())
        {
            if (entry.State == EntityState.Modified && entry.Entity.Status == ApplicationStatus.Submitted
                && entry.Property(x => x.Status).OriginalValue != ApplicationStatus.Submitted)
            {
                var item = entry.Entity;
                events.Add(OutboxMessage.ApplicationNotification("ApplicationSubmitted", item.Id,
                    item.StudentId, item.CompetitionId, item.SubmittedAtUtc!.Value));
            }
        }
        foreach (var entry in ChangeTracker.Entries<Ranking>())
        {
            if (entry.State != EntityState.Modified || entry.Entity.Status != RankingStatus.Published
                || entry.Property(x => x.Status).OriginalValue == RankingStatus.Published)
                continue;
            var ranking = entry.Entity;
            var type = ranking.Type == RankingType.Final ? "FinalRankingPublished" : "PreliminaryRankingPublished";
            foreach (var candidate in ranking.Entries)
                events.Add(OutboxMessage.ApplicationNotification(type, candidate.ApplicationId,
                    candidate.StudentId, ranking.CompetitionId, ranking.PublishedAtUtc!.Value));
        }
        foreach (var message in events)
            if (!OutboxMessages.Local.Any(existing => existing.Id == message.Id))
                OutboxMessages.Add(message);
        return base.SaveChangesAsync(cancellationToken);
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentApplication>().Property(x => x.Status).IsConcurrencyToken();
        modelBuilder.Entity<Competition>().Property(x => x.RowVersion).IsRowVersion();
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

        var ranking = modelBuilder.Entity<Ranking>();
        ranking.HasIndex(x => new { x.CompetitionId, x.Type }).IsUnique();
        ranking.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);
        ranking.Property(x => x.SourceFingerprint).HasMaxLength(64);
        ranking.Property(x => x.RowVersion).IsRowVersion();
        ranking.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.RankingId)
            .OnDelete(DeleteBehavior.Cascade);
        ranking.Navigation(x => x.Entries).HasField("entries").UsePropertyAccessMode(PropertyAccessMode.Field);

        var entry = modelBuilder.Entity<RankingEntry>();
        entry.Property(x => x.Id).ValueGeneratedNever();
        entry.Property(x => x.TotalPoints).HasPrecision(18, 2);
        entry.HasIndex(x => new { x.RankingId, x.ApplicationId }).IsUnique();
        entry.HasOne<StudentApplication>().WithMany().HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        var appeal = modelBuilder.Entity<Appeal>();
        appeal.HasIndex(x => x.ApplicationId).IsUnique();
        appeal.Property(x => x.Reason).HasMaxLength(4000);
        appeal.Property(x => x.Response).HasMaxLength(4000);
        appeal.Property(x => x.RowVersion).IsRowVersion();
        appeal.HasOne<StudentApplication>().WithMany().HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        appeal.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        var eligibility = modelBuilder.Entity<AccommodationEligibility>();
        eligibility.HasIndex(x => x.ApplicationId).IsUnique();
        eligibility.HasIndex(x => new { x.CompetitionId, x.StudentId }).IsUnique();
        eligibility.Property(x => x.AcademicYear).HasMaxLength(20);
        eligibility.HasOne<StudentApplication>().WithMany().HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        eligibility.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);
        eligibility.HasOne<Ranking>().WithMany().HasForeignKey(x => x.RankingId)
            .OnDelete(DeleteBehavior.Restrict);

        var message = modelBuilder.Entity<OutboxMessage>();
        message.Property(x => x.Type).HasMaxLength(100);
        message.HasIndex(x => x.EligibilityId).IsUnique();
        message.HasIndex(x => x.PublishedAtUtc);
        message.HasOne<AccommodationEligibility>().WithMany().HasForeignKey(x => x.EligibilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
