using Microsoft.EntityFrameworkCore;
using StudentCenter.NotificationService.Domain.Entities;

namespace StudentCenter.NotificationService.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ReceivedEvent> ReceivedEvents => Set<ReceivedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var received = modelBuilder.Entity<ReceivedEvent>();
        received.HasKey(message => message.Id);
        received.Property(message => message.Fingerprint).HasMaxLength(64).IsRequired();

        var notifications = modelBuilder.Entity<Notification>();
        notifications.HasKey(notification => notification.Id);
        notifications.Property(notification => notification.EventType).HasMaxLength(100).IsRequired();
        notifications.Property(notification => notification.RecipientKind).HasMaxLength(10).IsRequired();
        notifications.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        notifications.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
        notifications.Property(notification => notification.ResourceType).HasMaxLength(30).IsRequired();
        notifications.HasIndex(notification => new
        {
            notification.EventId, notification.RecipientKind, notification.RecipientId
        }).IsUnique();
        notifications.HasIndex(notification => new
        {
            notification.RecipientKind, notification.RecipientId, notification.ReadAtUtc, notification.OccurredAtUtc
        });
        notifications.HasOne<ReceivedEvent>().WithMany().HasForeignKey(notification => notification.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
