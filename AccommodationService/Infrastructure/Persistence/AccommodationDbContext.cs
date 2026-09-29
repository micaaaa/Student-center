using Microsoft.EntityFrameworkCore;
using StudentCenter.AccommodationService.Domain.Entities;

namespace StudentCenter.AccommodationService.Infrastructure.Persistence;

public sealed class AccommodationDbContext(DbContextOptions<AccommodationDbContext> options) : DbContext(options)
{
    public DbSet<Dorm> Dorms => Set<Dorm>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ReceivedEligibility> ReceivedEligibilities => Set<ReceivedEligibility>();
    public DbSet<StudentAccommodation> StudentAccommodations => Set<StudentAccommodation>();
    public DbSet<MoveIn> MoveIns => Set<MoveIn>();
    public DbSet<MoveOut> MoveOuts => Set<MoveOut>();
    public DbSet<AccommodationOutboxMessage> OutboxMessages => Set<AccommodationOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var message = modelBuilder.Entity<AccommodationOutboxMessage>();
        message.Property(item => item.Type).HasMaxLength(100);
        message.Property(item => item.Sequence).UseIdentityColumn();
        message.HasIndex(item => item.Sequence).IsUnique();
        message.HasIndex(item => new { item.AccommodationId, item.Type }).IsUnique();
        message.HasIndex(item => new { item.PublishedAtUtc, item.Sequence });
        message.HasOne<StudentAccommodation>().WithMany()
            .HasForeignKey(item => item.AccommodationId).OnDelete(DeleteBehavior.Restrict);

        var dorm = modelBuilder.Entity<Dorm>();
        dorm.Property(item => item.Name).HasMaxLength(200);
        dorm.Property(item => item.Address).HasMaxLength(250);
        dorm.Property(item => item.City).HasMaxLength(100);
        dorm.Property(item => item.Category).HasMaxLength(100);
        dorm.Property(item => item.RowVersion).IsRowVersion();
        dorm.ToTable("Dorms", table => table.HasCheckConstraint("CK_Dorms_Capacity", "[Capacity] > 0"));

        var room = modelBuilder.Entity<Room>();
        room.Property(item => item.RoomNumber).HasMaxLength(20);
        room.Property(item => item.RowVersion).IsRowVersion();
        room.HasIndex(item => new { item.DormId, item.RoomNumber }).IsUnique();
        room.HasOne<Dorm>().WithMany().HasForeignKey(item => item.DormId).OnDelete(DeleteBehavior.Restrict);
        room.ToTable("Rooms", table => table.HasCheckConstraint(
            "CK_Rooms_Capacity", "[Capacity] > 0 AND [OccupiedBeds] >= 0 AND [OccupiedBeds] <= [Capacity]"));

        var eligibility = modelBuilder.Entity<ReceivedEligibility>();
        eligibility.Property(item => item.Id).ValueGeneratedNever();
        eligibility.Property(item => item.AcademicYear).HasMaxLength(20);
        eligibility.HasIndex(item => item.EventId).IsUnique();
        eligibility.HasIndex(item => item.CompetitionId);

        var assignment = modelBuilder.Entity<StudentAccommodation>();
        assignment.Property(item => item.AcademicYear).HasMaxLength(20);
        assignment.Property(item => item.Status).HasMaxLength(20);
        assignment.Property(item => item.CancellationReason).HasMaxLength(1000);
        assignment.Property(item => item.RowVersion).IsRowVersion();
        assignment.HasIndex(item => item.StudentId).IsUnique().HasFilter("[IsActive] = 1");
        assignment.HasOne<ReceivedEligibility>().WithMany()
            .HasForeignKey(item => item.EligibilityId).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<Room>().WithMany()
            .HasForeignKey(item => item.RoomId).OnDelete(DeleteBehavior.Restrict);

        var moveIn = modelBuilder.Entity<MoveIn>();
        moveIn.Property(item => item.Id).ValueGeneratedNever();
        moveIn.Property(item => item.DateUtc).HasConversion(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        moveIn.Property(item => item.MedicalCertificateReference).HasMaxLength(250);
        assignment.HasOne(item => item.MoveIn).WithOne()
            .HasForeignKey<MoveIn>(item => item.AccommodationId).OnDelete(DeleteBehavior.Restrict);

        var moveOut = modelBuilder.Entity<MoveOut>();
        moveOut.Property(item => item.Id).ValueGeneratedNever();
        moveOut.Property(item => item.DateUtc).HasConversion(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        moveOut.Property(item => item.Reason).HasMaxLength(1000);
        assignment.HasOne(item => item.MoveOut).WithOne()
            .HasForeignKey<MoveOut>(item => item.AccommodationId).OnDelete(DeleteBehavior.Restrict);
    }
}
