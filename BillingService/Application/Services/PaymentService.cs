using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Exceptions;

namespace StudentCenter.BillingService.Application.Services;

public sealed class PaymentService(IPaymentRepository repository, IBillingStudentClient students, TimeProvider clock)
{
    public Task<PaymentResult> RecordAsync(RecordPaymentRequest request, Guid actorId, CancellationToken ct)
    {
        Payment.Validate(request.RequestId, request.ChargeId, request.Amount, request.Method, request.ReferenceNumber, actorId);
        var reference = Payment.NormalizeReference(request.ReferenceNumber);
        return repository.InTransactionAsync(async token =>
        {
            var existing = await repository.GetByRequestAsync(request.RequestId, token);
            if (existing is not null)
            {
                if (existing.ChargeId != request.ChargeId || existing.Amount != request.Amount
                    || existing.Method != request.Method || existing.ReferenceNumber != reference)
                {
                    throw new BillingConflictException("Request ID was already used for a different payment.");
                }

                return new PaymentResult(PaymentResponse.From(existing), true);
            }

            var charge = await repository.GetChargeAsync(request.ChargeId, token)
                ?? throw new KeyNotFoundException("Charge not found.");
            var payment = charge.RecordPayment(request.RequestId, request.Amount, request.Method,
                reference, actorId, clock.GetUtcNow());
            await repository.AddAsync(payment, token);
            await repository.SaveAsync(token);
            return new PaymentResult(PaymentResponse.From(payment), false);
        }, ct);
    }

    public async Task<PaymentResponse> GetAsync(Guid id, CancellationToken ct)
    {
        return PaymentResponse.From(await FindAsync(id, null, ct));
    }

    public async Task<PaymentResponse> GetMineAsync(Guid id, CancellationToken ct)
    {
        return PaymentResponse.From(await FindAsync(id, await students.GetCurrentStudentIdAsync(ct), ct));
    }

    public async Task<IReadOnlyCollection<PaymentResponse>> ListAsync(
        Guid studentId, Guid? chargeId, int page, int pageSize, CancellationToken ct)
    {
        if (studentId == Guid.Empty || chargeId == Guid.Empty)
        {
            throw new ArgumentException("Valid student and charge identifiers are required.");
        }

        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        }

        return (await repository.ListAsync(studentId, chargeId, page, pageSize, ct)).Select(PaymentResponse.From).ToArray();
    }

    public async Task<IReadOnlyCollection<PaymentResponse>> ListMineAsync(
        Guid? chargeId, int page, int pageSize, CancellationToken ct)
    {
        return await ListAsync(await students.GetCurrentStudentIdAsync(ct), chargeId, page, pageSize, ct);
    }

    private async Task<Payment> FindAsync(Guid id, Guid? studentId, CancellationToken ct)
    {
        return await repository.GetAsync(id, studentId, ct) ?? throw new KeyNotFoundException("Payment not found.");
    }
}
