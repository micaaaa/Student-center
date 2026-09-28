using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class CompetitionService(ICompetitionRepository repository)
{
    public async Task<CompetitionResponse> CreateAsync(CreateCompetitionRequest r, CancellationToken ct)
    {
        ValidateDates(r.ApplicationStartDateUtc, r.ApplicationEndDateUtc);
        var c = new Competition(
            r.AcademicYear.Trim(),
            r.Name.Trim(),
            r.Description?.Trim(),
            r.ApplicationStartDateUtc,
            r.ApplicationEndDateUtc);
        await repository.AddAsync(c, ct);
        return Map(c);
    }

    public async Task<IReadOnlyCollection<CompetitionResponse>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(Map).ToArray();

    public async Task<CompetitionResponse> GetAsync(Guid id, CancellationToken ct) => Map(await Find(id, ct));

    public async Task<CompetitionResponse> UpdateAsync(Guid id, UpdateCompetitionRequest r, CancellationToken ct)
    {
        ValidateDates(r.ApplicationStartDateUtc, r.ApplicationEndDateUtc);
        var c = await Find(id, ct);
        c.Update(r.Name.Trim(), r.Description?.Trim(), r.ApplicationStartDateUtc, r.ApplicationEndDateUtc);
        await repository.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<CompetitionResponse> OpenAsync(Guid id, CancellationToken ct)
    {
        var c = await Find(id, ct);
        c.Open();
        await repository.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<CompetitionResponse> CloseAsync(Guid id, CancellationToken ct)
    {
        var c = await Find(id, ct);
        c.Close();
        await repository.SaveChangesAsync(ct);
        return Map(c);
    }

    private async Task<Competition> Find(Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Competition was not found.");

    private static void ValidateDates(DateTime a, DateTime b)
    {
        if (a >= b)
            throw new ArgumentException("Application start date must be before end date.");
    }

    private static CompetitionResponse Map(Competition c) => new(
        c.Id,
        c.AcademicYear,
        c.Name,
        c.Description,
        c.ApplicationStartDateUtc,
        c.ApplicationEndDateUtc,
        c.Status.ToString().ToUpperInvariant());
}
