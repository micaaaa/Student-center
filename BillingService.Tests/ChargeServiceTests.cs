using NUnit.Framework;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Application.Services;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;
using StudentCenter.BillingService.Domain.Exceptions;

namespace StudentCenter.BillingService.Tests;

[TestFixture]
public sealed class ChargeServiceTests
{
    private static CreateChargeRequest Request()
    {
        return new CreateChargeRequest
        {
            RequestId = Guid.NewGuid(), StudentId = Guid.NewGuid(), Type = ChargeType.Other,
            Amount = 100, DueDate = new DateOnly(2026, 10, 10), Description = "Naknada"
        };
    }

    [Test]
    public async Task RetryReturnsOriginalChargeWithoutAnotherStudentLookup()
    {
        var repository = new Repository();
        var students = new Students();
        var service = new ChargeService(repository, students, TimeProvider.System);
        var request = Request();
        var first = await service.CreateAsync(request, Guid.NewGuid(), default);
        var second = await service.CreateAsync(request, Guid.NewGuid(), default);
        Assert.That(second.AlreadyExists, Is.True);
        Assert.That(second.Charge.Id, Is.EqualTo(first.Charge.Id));
        Assert.That(repository.Charges, Has.Count.EqualTo(1));
        Assert.That(students.Lookups, Is.EqualTo(1));
        request.Amount = 200;
        Assert.ThrowsAsync<BillingConflictException>(() => service.CreateAsync(request, Guid.NewGuid(), default));
    }

    [Test]
    public void MissingStudentDoesNotSaveCharge()
    {
        var repository = new Repository();
        var service = new ChargeService(repository, new Students { Missing = true }, TimeProvider.System);
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(Request(), Guid.NewGuid(), default));
        Assert.That(repository.Charges, Is.Empty);
    }

    [Test]
    public async Task StudentCannotReadAnotherStudentsCharge()
    {
        var repository = new Repository();
        var service = new ChargeService(repository, new Students(), TimeProvider.System);
        var created = await service.CreateAsync(Request(), Guid.NewGuid(), default);
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetMineAsync(created.Charge.Id, default));
    }

    private sealed class Students : IBillingStudentClient
    {
        public bool Missing { get; init; }
        public int Lookups { get; private set; }
        public Guid Current { get; } = Guid.NewGuid();

        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct)
        {
            return Task.FromResult(Current);
        }

        public Task EnsureExistsAsync(Guid studentId, CancellationToken ct)
        {
            Lookups++;
            if (Missing)
            {
                throw new KeyNotFoundException();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class Repository : IBillingRepository
    {
        public List<Charge> Charges { get; } = [];

        public Task ReceiveAsync(IncomingBillingEvent message, DateTimeOffset now, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task<Charge?> GetByRequestAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(Charges.SingleOrDefault(charge => charge.RequestId == id));
        }

        public Task<Charge> CreateAsync(Charge charge, CancellationToken ct)
        {
            Charges.Add(charge);
            return Task.FromResult(charge);
        }

        public Task<Charge?> GetAsync(Guid id, Guid? studentId, CancellationToken ct)
        {
            return Task.FromResult(Charges.SingleOrDefault(charge => charge.Id == id
                && (!studentId.HasValue || charge.StudentId == studentId)));
        }

        public Task<IReadOnlyCollection<Charge>> ListAsync(Guid studentId, bool overdueOnly,
            DateOnly today, int page, int pageSize, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task<BalanceResponse> BalanceAsync(Guid studentId, DateOnly today, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}

