using System.Globalization;
using System.Text.Json;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Interfaces;
using StudentCenter.BillingService.Domain.Entities;
using StudentCenter.BillingService.Domain.Enums;
using StudentCenter.BillingService.Domain.Exceptions;

namespace StudentCenter.BillingService.Application.Services;

public sealed class ChargeService(IBillingRepository repository, IBillingStudentClient students, TimeProvider clock)
{
    public async Task<ChargeResult> CreateAsync(CreateChargeRequest request, Guid actorId, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || actorId == Guid.Empty || request.Type == ChargeType.Meal)
        {
            throw new ArgumentException("Request and actor IDs are required. Meal purchases are already paid and cannot create an outstanding charge.");
        }

        var referenceId = request.ReferenceId == Guid.Empty && request.Type != ChargeType.Accommodation
            ? request.RequestId : request.ReferenceId;
        string? period = null;
        if (request.Type == ChargeType.Accommodation)
        {
            if (request.Period is null || request.Period.Length != 7
                || !DateOnly.TryParseExact(request.Period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
            {
                throw new ArgumentException("Accommodation charges require a YYYY-MM billing period.");
            }

            period = request.Period;
        }
        else if (!string.IsNullOrEmpty(request.Period))
        {
            throw new ArgumentException("Billing period is only supported for accommodation charges.");
        }

        var description = request.Description?.Trim() ?? string.Empty;
        var fingerprint = BillingEventParser.Hash(JsonSerializer.Serialize(new
        {
            request.StudentId, request.Type, request.Amount, request.DueDate, referenceId, period, description
        }));
        var sourceKey = request.Type == ChargeType.Accommodation
            ? $"accommodation:{referenceId}:{period}" : $"manual:{request.RequestId}";
        var charge = new Charge(request.StudentId, request.Type, request.Amount, request.DueDate,
            referenceId, description, sourceKey, request.RequestId, fingerprint, actorId, clock.GetUtcNow(), period);
        var previous = await repository.GetByRequestAsync(request.RequestId, ct);
        if (previous is not null)
        {
            if (previous.SourceFingerprint != fingerprint)
            {
                throw new BillingConflictException("Request ID was already used for a different charge.");
            }

            return new ChargeResult(ChargeResponse.From(previous, Today()), true);
        }

        await students.EnsureExistsAsync(request.StudentId, ct);
        var saved = await repository.CreateAsync(charge, ct);
        return new ChargeResult(ChargeResponse.From(saved, Today()), saved.Id != charge.Id);
    }

    public async Task<ChargeResponse> GetAsync(Guid id, CancellationToken ct)
    {
        return ChargeResponse.From(await FindAsync(id, null, ct), Today());
    }

    public async Task<ChargeResponse> GetMineAsync(Guid id, CancellationToken ct)
    {
        return ChargeResponse.From(await FindAsync(id, await students.GetCurrentStudentIdAsync(ct), ct), Today());
    }

    public async Task<IReadOnlyCollection<ChargeResponse>> ListAsync(
        Guid studentId, bool overdueOnly, int page, int pageSize, CancellationToken ct)
    {
        ValidateStudent(studentId);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentException("Page must be positive and page size must be between 1 and 100.");
        }

        var today = Today();
        return (await repository.ListAsync(studentId, overdueOnly, today, page, pageSize, ct))
            .Select(charge => ChargeResponse.From(charge, today)).ToArray();
    }

    public async Task<IReadOnlyCollection<ChargeResponse>> ListMineAsync(
        bool overdueOnly, int page, int pageSize, CancellationToken ct)
    {
        return await ListAsync(await students.GetCurrentStudentIdAsync(ct), overdueOnly, page, pageSize, ct);
    }

    public Task<BalanceResponse> BalanceAsync(Guid studentId, CancellationToken ct)
    {
        ValidateStudent(studentId);
        return repository.BalanceAsync(studentId, Today(), ct);
    }

    public async Task<BalanceResponse> MyBalanceAsync(CancellationToken ct)
    {
        return await BalanceAsync(await students.GetCurrentStudentIdAsync(ct), ct);
    }

    private async Task<Charge> FindAsync(Guid id, Guid? studentId, CancellationToken ct)
    {
        return await repository.GetAsync(id, studentId, ct) ?? throw new KeyNotFoundException("Charge not found.");
    }

    private DateOnly Today()
    {
        return DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    }

    private static void ValidateStudent(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Student identifier is required.");
        }
    }
}

