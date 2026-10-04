using Microsoft.EntityFrameworkCore;
using StudentCenter.BillingService.Domain.Entities;

namespace StudentCenter.BillingService.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<BillingOutboxMessage> OutboxMessages => Set<BillingOutboxMessage>();
    public DbSet<ReceivedEvent> ReceivedEvents => Set<ReceivedEvent>();
    public DbSet<AccommodationReference> AccommodationReferences => Set<AccommodationReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var outbox = modelBuilder.Entity<BillingOutboxMessage>();
        outbox.HasKey(message => message.Id);
        outbox.Property(message => message.Sequence).UseIdentityColumn();
        outbox.HasIndex(message => message.Sequence).IsUnique();
        outbox.HasIndex(message => new { message.Type, message.SourceId }).IsUnique();
        outbox.HasIndex(message => new { message.PublishedAtUtc, message.Sequence });
        outbox.Property(message => message.Type).HasMaxLength(100).IsRequired();
        outbox.Property(message => message.Payload).IsRequired();
        outbox.HasOne<Charge>().WithMany().HasForeignKey(message => message.ChargeId).OnDelete(DeleteBehavior.Restrict);
        outbox.HasOne<Payment>().WithMany().HasForeignKey(message => message.PaymentId).OnDelete(DeleteBehavior.Restrict);

        var charges = modelBuilder.Entity<Charge>();
        charges.HasKey(charge => charge.Id);
        charges.Property(charge => charge.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        charges.Property(charge => charge.Amount).HasPrecision(18, 2);
        charges.Property(charge => charge.PaidAmount).HasPrecision(18, 2);
        charges.Property(charge => charge.RowVersion).IsRowVersion();
        charges.Ignore(charge => charge.OutstandingAmount);
        charges.Property(charge => charge.Description).HasMaxLength(1000).IsRequired();
        charges.Property(charge => charge.SourceKey).HasMaxLength(150).IsRequired();
        charges.Property(charge => charge.SourceFingerprint).HasMaxLength(64).IsRequired();
        charges.Property(charge => charge.Period).HasMaxLength(7);
        charges.HasIndex(charge => charge.SourceKey).IsUnique();
        charges.HasIndex(charge => charge.RequestId).IsUnique();
        charges.HasIndex(charge => new { charge.StudentId, charge.DueDate });
        charges.ToTable(table => table.HasCheckConstraint("CK_Charges_PositiveAmount", "[Amount] > 0"));
        charges.ToTable(table => table.HasCheckConstraint("CK_Charges_PaidAmount", "[PaidAmount] >= 0 AND [PaidAmount] <= [Amount]"));

        var payments = modelBuilder.Entity<Payment>();
        payments.HasKey(payment => payment.Id);
        payments.Property(payment => payment.Amount).HasPrecision(18, 2);
        payments.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        payments.Property(payment => payment.ReferenceNumber).HasMaxLength(100);
        payments.HasIndex(payment => payment.RequestId).IsUnique();
        payments.HasIndex(payment => new { payment.Method, payment.ReferenceNumber }).IsUnique()
            .HasFilter("[ReferenceNumber] IS NOT NULL");
        payments.HasIndex(payment => new { payment.StudentId, payment.PaymentDateUtc });
        payments.HasOne<Charge>().WithMany().HasForeignKey(payment => payment.ChargeId).OnDelete(DeleteBehavior.Restrict);
        payments.ToTable(table => table.HasCheckConstraint("CK_Payments_PositiveAmount", "[Amount] > 0"));

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
