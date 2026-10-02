using NUnit.Framework;
using StudentCenter.NotificationService.Application.DTOs;
using StudentCenter.NotificationService.Application.Interfaces;
using StudentCenter.NotificationService.Domain.Entities;
using Service = StudentCenter.NotificationService.Application.Services.NotificationService;

namespace StudentCenter.NotificationService.Tests;

[TestFixture]
public sealed class NotificationAccessTests
{
    [TestCase(0, 50)]
    [TestCase(1, 0)]
    [TestCase(1, 101)]
    [TestCase(int.MaxValue, 100)]
    public void InvalidPaginationIsRejectedBeforeOwnerLookup(int page, int pageSize)
    {
        var service = new Service(new Repository(), new Owner(), TimeProvider.System);
        Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(false, page, pageSize, default));
    }

    [Test]
    public async Task AllOperationsUseAuthenticatedOwner()
    {
        var owner = new Owner();
        var repository = new Repository();
        var service = new Service(repository, owner, TimeProvider.System);
        await service.ListAsync(true, 2, 10, default);
        await service.CountUnreadAsync(default);
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(Guid.NewGuid(), default));
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.MarkReadAsync(Guid.NewGuid(), default));
        Assert.That(repository.Owners, Has.Count.EqualTo(4));
        Assert.That(repository.Owners.All(item => item == owner.Value), Is.True);
    }

    private sealed class Owner : ICurrentNotificationOwner
    {
        public NotificationOwner Value { get; } = new(Guid.NewGuid(), Guid.NewGuid());

        public Task<NotificationOwner> GetAsync(CancellationToken ct)
        {
            return Task.FromResult(Value);
        }
    }

    private sealed class Repository : INotificationRepository
    {
        public List<NotificationOwner> Owners { get; } = [];

        public Task ReceiveAsync(PreparedEvent message, DateTime receivedAtUtc, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<Notification>> ListAsync(
            NotificationOwner owner, bool unreadOnly, int page, int pageSize, CancellationToken ct)
        {
            Owners.Add(owner);
            return Task.FromResult<IReadOnlyCollection<Notification>>(Array.Empty<Notification>());
        }

        public Task<int> CountUnreadAsync(NotificationOwner owner, CancellationToken ct)
        {
            Owners.Add(owner);
            return Task.FromResult(0);
        }

        public Task<Notification?> GetAsync(Guid id, NotificationOwner owner, CancellationToken ct)
        {
            Owners.Add(owner);
            return Task.FromResult<Notification?>(null);
        }

        public Task<bool> MarkReadAsync(Guid id, NotificationOwner owner, DateTime now, CancellationToken ct)
        {
            Owners.Add(owner);
            return Task.FromResult(false);
        }
    }
}
