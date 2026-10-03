using Microsoft.EntityFrameworkCore;
using StudentCenter.BillingService.Domain.Entities;

namespace StudentCenter.BillingService.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<ReceivedEvent> ReceivedEvents => Set<ReceivedEvent>();
    public DbSet<AccommodationReference> AccommodationReferences => Set<AccommodationReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var charges = modelBuilder.Entity<Charge>();
        charges.HasKey(charge => charge.Id);
        charges.Property(charge => charge.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        charges.Property(charge => charge.Amount).HasPrecision(18, 2);
        charges.Property(charge => charge.Description).HasMaxLength(1000).IsRequired();
        charges.Property(charge => charge.SourceKey).HasMaxLength(150).IsRequired();
        charges.Property(charge => charge.SourceFingerprint).HasMaxLength(64).IsRequired();
        charges.Property(charge => charge.Period).HasMaxLength(7);
        charges.HasIndex(charge => charge.SourceKey).IsUnique();
        charges.HasIndex(charge => charge.RequestId).IsUnique();
        charges.HasIndex(charge => new { charge.StudentId, charge.DueDate });
        charges.ToTable(table => table.HasCheckConstraint("CK_Charges_PositiveAmount", "[Amount] > 0"));

        var receipts = modelBuilder.Entity<ReceivedEvent>();
        receipts.HasKey(receipt => receipt.Id);
        receipts.Property(receipt => receipt.Fingerprint).HasMaxLength(64).IsRequired();

        var references = modelBuilder.Entity<AccommodationReference>();
        references.HasKey(reference => reference.Id);
        references.Property(reference => reference.AcademicYear).HasMaxLength(9).IsRequired();
        references.Property(reference => reference.RowVersion).IsRowVersion();
        references.HasIndex(reference => reference.StudentId);
    }
}
