using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class ApplicationService(IApplicationRepository applications, ICompetitionRepository competitions, IStudentClient students)
{
    public async Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        await EnsureCompetitionAcceptsApplicationsAsync(request.CompetitionId, ct);
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        if (await applications.GetForStudentAsync(request.CompetitionId, studentId, ct) is not null)
            throw new ApplicationConflictException("You already have an application for this competition.");
        var application = new StudentApplication(request.CompetitionId, studentId, request.Note);
        await applications.AddAsync(application, ct);
        return Map(application);
    }

    public async Task<IReadOnlyCollection<ApplicationResponse>> GetMineAsync(CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return (await applications.GetMineAsync(studentId, ct)).Select(Map).ToArray();
    }

    public async Task<ApplicationResponse> GetAsync(Guid id, CancellationToken ct) => Map(await FindOwnedAsync(id, ct));

    public async Task<ApplicationResponse> UpdateAsync(Guid id, UpdateApplicationRequest request, CancellationToken ct)
    {
        var application = await FindOwnedAsync(id, ct);
        application.UpdateNote(request.Note);
        await applications.SaveAsync(ct);
        return Map(application);
    }

    public async Task<ApplicationResponse> SubmitAsync(Guid id, CancellationToken ct)
    {
        var application = await FindOwnedAsync(id, ct);
        await EnsureCompetitionAcceptsApplicationsAsync(application.CompetitionId, ct);
        application.Submit();
        await applications.SaveAsync(ct);
        return Map(application);
    }

    public async Task<ApplicationResponse> WithdrawAsync(Guid id, CancellationToken ct)
    {
        var application = await FindOwnedAsync(id, ct);
        application.Withdraw();
        await applications.SaveAsync(ct);
        return Map(application);
    }

    private async Task<StudentApplication> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        var application = await applications.GetAsync(id, ct);
        if (application is null || application.StudentId != studentId)
            throw new KeyNotFoundException("Application was not found.");
        return application;
    }

    private async Task EnsureCompetitionAcceptsApplicationsAsync(Guid id, CancellationToken ct)
    {
        var competition = await competitions.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Competition was not found.");
        var now = DateTime.UtcNow;
        if (competition.Status != CompetitionStatus.Open)
            throw new ApplicationConflictException("Applications are allowed only for an open competition.");
        if (now < competition.ApplicationStartDateUtc || now > competition.ApplicationEndDateUtc)
            throw new ApplicationConflictException("The competition application period is not active.");
    }

    private static ApplicationResponse Map(StudentApplication application) => new(
        application.Id,
        application.CompetitionId,
        application.StudentId,
        application.Status.ToString().ToUpperInvariant(),
        application.CreatedAtUtc,
        application.SubmittedAtUtc,
        application.Note);
}
