using Microsoft.EntityFrameworkCore;
using StudentCenter.AccommodationService.Domain.Entities;

namespace StudentCenter.AccommodationService.Infrastructure.Persistence;

public sealed class AccommodationDbContext(DbContextOptions<AccommodationDbContext> options) : DbContext(options)
{
    public DbSet<Dorm> Dorms => Set<Dorm>();
    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
