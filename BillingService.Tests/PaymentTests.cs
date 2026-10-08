using NUnit.Framework;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Application.Services;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;
using StudentCenter.BillingService.Domain.Exceptions;

namespace StudentCenter.BillingService.Tests;

[TestFixture]
public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.NewGuid();

    private static Charge NewCharge(bool overdue = false)
    {
        return new Charge(Guid.NewGuid(), ChargeType.Other, 100, new DateOnly(2026, 10, overdue ? 3 : 5),
            Guid.NewGuid(), "Naknada", "manual:" + Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), ActorId, Now);
    }

    [Test]
    public void PartialThenFullPaymentUpdatesRemainingAmountAndStatus()
    {
        var charge = NewCharge();
        var first = charge.RecordPayment(Guid.NewGuid(), 30.25m, PaymentMethod.Cash, null, ActorId, Now);
        Assert.Multiple(() =>
        {
            Assert.That(charge.PaidAmount, Is.EqualTo(30.25m));
            Assert.That(charge.OutstandingAmount, Is.EqualTo(69.75m));
            Assert.That(charge.Status(new DateOnly(2026, 10, 4)), Is.EqualTo("PARTIALLY_PAID"));
            Assert.That(charge.PaidAtUtc, Is.Null);
            Assert.That(first.StudentId, Is.EqualTo(charge.StudentId));
            Assert.That(first.RecordedByUserId, Is.EqualTo(ActorId));
        });
        charge.RecordPayment(Guid.NewGuid(), 69.75m, PaymentMethod.BankTransfer, " tx-123 ", ActorId, Now);
        Assert.That(charge.Status(new DateOnly(2026, 10, 10)), Is.EqualTo("PAID"));
        Assert.That(charge.OutstandingAmount, Is.Zero);
        Assert.That(charge.PaidAtUtc, Is.EqualTo(Now.UtcDateTime));
        Assert.That(first.Amount, Is.EqualTo(30.25m));
    }

    [Test]
    public void OverpaymentAndPaymentOnPaidChargeDoNotMutateCharge()
    {
        var charge = NewCharge();
        Assert.Throws<BillingConflictException>(() => charge.RecordPayment(Guid.NewGuid(), 100.01m, PaymentMethod.Cash, null, ActorId, Now));
        Assert.That(charge.PaidAmount, Is.Zero);
        charge.RecordPayment(Guid.NewGuid(), 100, PaymentMethod.Cash, null, ActorId, Now);
        Assert.Throws<BillingConflictException>(() => charge.RecordPayment(Guid.NewGuid(), 0.01m, PaymentMethod.Cash, null, ActorId, Now));
        Assert.That(charge.PaidAmount, Is.EqualTo(100));
    }

    [Test]
    public async Task RetryOnFullyPaidChargeReturnsOriginalPayment()
    {
        var repository = new MemoryRepository(NewCharge());
        var service = new PaymentService(repository, new Students(repository.Charge.StudentId), new FixedClock());
        var request = Request(repository.Charge, 100);
        request.Method = PaymentMethod.BankTransfer;
        request.ReferenceNumber = " tx-123 ";
        var first = await service.RecordAsync(request, ActorId, default);
        request.ReferenceNumber = "TX-123";
        var retry = await service.RecordAsync(request, Guid.NewGuid(), default);
        Assert.That(retry.AlreadyExists, Is.True);
        Assert.That(retry.Payment.Id, Is.EqualTo(first.Payment.Id));
        Assert.That(repository.Payments, Has.Count.EqualTo(1));
        Assert.That(repository.Saves, Is.EqualTo(1));
        Assert.That(repository.Charge.PaidAmount, Is.EqualTo(100));
    }

    [TestCase("amount")]
    [TestCase("charge")]
    public async Task ReusedRequestWithDifferentDataIsRejected(string field)
    {
        var repository = new MemoryRepository(NewCharge());
        var service = new PaymentService(repository, new Students(repository.Charge.StudentId), new FixedClock());
        var request = Request(repository.Charge, 10);
        await service.RecordAsync(request, ActorId, default);
        switch (field)
        {
            case "amount": request.Amount = 20; break;
            case "charge": request.ChargeId = Guid.NewGuid(); break;
            case "method": request.Method = PaymentMethod.Card; request.ReferenceNumber = "TX-5"; break;
            case "reference": request.ReferenceNumber = "receipt-5"; break;
        }
        Assert.ThrowsAsync<BillingConflictException>(() => service.RecordAsync(request, ActorId, default));
        Assert.That(repository.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task StudentCannotSeeAnotherStudentsPaymentOrHistory()
    {
        var repository = new MemoryRepository(NewCharge());
        var service = new PaymentService(repository, new Students(Guid.NewGuid()), new FixedClock());
        var recorded = await service.RecordAsync(Request(repository.Charge, 10), ActorId, default);
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetMineAsync(recorded.Payment.Id, default));
        Assert.That(await service.ListMineAsync(repository.Charge.Id, 1, 50, default), Is.Empty);
    }

    private static RecordPaymentRequest Request(Charge charge, decimal amount)
    {
        return new RecordPaymentRequest { RequestId = Guid.NewGuid(), ChargeId = charge.Id, Amount = amount, Method = PaymentMethod.Cash };
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Students(Guid id) : IBillingStudentClient
    {
        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct) => Task.FromResult(id);
        public Task EnsureExistsAsync(Guid studentId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class MemoryRepository(Charge charge) : IPaymentRepository
    {
        public Charge Charge { get; } = charge;
        public List<Payment> Payments { get; } = [];
        public int Saves { get; private set; }

        public Task<Payment?> GetByRequestAsync(Guid requestId, CancellationToken ct)
        {
            return Task.FromResult(Payments.SingleOrDefault(payment => payment.RequestId == requestId));
        }

        public Task<Charge?> GetChargeAsync(Guid chargeId, CancellationToken ct)
        {
            return Task.FromResult<Charge?>(Charge.Id == chargeId ? Charge : null);
        }

        public Task AddAsync(Payment payment, CancellationToken ct)
        {
            Payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            return action(ct);
        }

        public Task<Payment?> GetAsync(Guid id, Guid? studentId, CancellationToken ct)
        {
            return Task.FromResult(Payments.SingleOrDefault(payment => payment.Id == id
                && (!studentId.HasValue || payment.StudentId == studentId)));
        }

        public Task<IReadOnlyCollection<Payment>> ListAsync(Guid studentId, Guid? chargeId, int page, int pageSize, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyCollection<Payment>>(Payments.Where(payment => payment.StudentId == studentId
                && (!chargeId.HasValue || payment.ChargeId == chargeId)).Skip((page - 1) * pageSize).Take(pageSize).ToArray());
        }
    }
}
