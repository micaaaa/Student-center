using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Entities;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;
using StudentCenter.MaintenanceService.Infrastructure.Repositories;

namespace MaintenanceService.Tests;

[TestFixture]
public sealed class OutboxTests
{
    private static (MaintenanceRequest Request, MaintenanceWorker Worker, MaintenanceAction Action) Assignment()
    {
        var worker = new MaintenanceWorker(Guid.NewGuid(), "Serviser", "Vodoinstalater");
        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new MaintenanceCategory("Voda", null), "Kvar", "Opis kvara",
            StudentCenter.MaintenanceService.Domain.Enums.RequestPriority.High, DateTimeOffset.UtcNow);
        request.Accept(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var action = request.Assign(worker, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return (request, worker, action);
    }

    [Test]
    public async Task BrokerFailureLeavesPendingMessageForRetryWithSameId()
    {
        var (request, worker, action) = Assignment();
        var message = MaintenanceOutboxMessage.From(action, request, worker);
        var repository = new MemoryOutbox(message);
        var publisher = new Publisher { Fail = true };
        var dispatcher = new MaintenanceOutboxDispatcher(repository, publisher, TimeProvider.System);
        Assert.ThrowsAsync<IOException>(() => dispatcher.DispatchAsync(default));
        Assert.That(message.PublishedAtUtc, Is.Null);
        Assert.That(repository.Saves, Is.Zero);
        publisher.Fail = false;
        await dispatcher.DispatchAsync(default);
        await dispatcher.DispatchAsync(default);
        Assert.That(publisher.Ids, Is.EqualTo(new[] { message.Id, message.Id }));
        Assert.That(repository.Saves, Is.EqualTo(1));
        Assert.That(message.PublishedAtUtc, Is.Not.Null);
    }

    private sealed class MemoryOutbox(MaintenanceOutboxMessage message) : IMaintenanceOutboxRepository
    {
        public int Saves { get; private set; }

        public Task<IReadOnlyCollection<MaintenanceOutboxMessage>> GetPendingAsync(CancellationToken ct)
        {
            IReadOnlyCollection<MaintenanceOutboxMessage> pending = message.PublishedAtUtc is null ? [message] : [];
            return Task.FromResult(pending);
        }

        public Task SaveAsync(CancellationToken ct)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class Publisher : IMaintenanceEventPublisher
    {
        public bool Fail { get; set; }
        public List<Guid> Ids { get; } = [];

        public Task PublishAsync(MaintenanceOutboxMessage message, CancellationToken ct)
        {
            Ids.Add(message.Id);
            if (Fail)
            {
                throw new IOException("Broker unavailable");
            }

            return Task.CompletedTask;
        }
    }
}
