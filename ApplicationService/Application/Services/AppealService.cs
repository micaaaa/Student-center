using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Domain.Services;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class AppealService(IConclusionRepository repository, IStudentClient students, TimeProvider clock)
{
    public async Task<AppealResponse> SubmitAsync(Guid applicationId, string reason, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return await repository.InTransactionAsync(async token =>
        {
            var application = await OwnedApplicationAsync(applicationId, studentId, token);
            var competition = await MutableCompetitionAsync(application.CompetitionId, token);
            var preliminary = await repository.GetPreliminaryAsync(competition.Id, token);
            var now = clock.GetUtcNow().UtcDateTime;
            if (preliminary?.Status != RankingStatus.Published
                || competition.AppealDeadlineUtc is null || now > competition.AppealDeadlineUtc)
                throw new ApplicationConflictException("The appeal period is not open.");
            if (!preliminary.Entries.Any(entry => entry.ApplicationId == applicationId))
                throw new ApplicationConflictException("Only applications on the preliminary ranking can be appealed.");
            if ((await repository.GetAppealsAsync(competition.Id, token))
                .Any(appeal => appeal.ApplicationId == applicationId))
                throw new ApplicationConflictException("An appeal already exists for this application.");

            var appeal = new Appeal(applicationId, competition.Id, studentId, reason, now);
            await repository.AddAppealAsync(appeal, token);
            await repository.SaveAsync(token);
            return Map(appeal);
        }, ct);
    }

    public async Task<AppealResponse> GetMineAsync(Guid applicationId, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        var application = await OwnedApplicationAsync(applicationId, studentId, ct);
        var appeal = (await repository.GetAppealsAsync(application.CompetitionId, ct))
            .SingleOrDefault(item => item.ApplicationId == applicationId)
            ?? throw new KeyNotFoundException("Appeal was not found.");
        return Map(appeal);
    }

    public async Task<IReadOnlyCollection<AppealResponse>> ListAsync(Guid competitionId, CancellationToken ct)
    {
        _ = await repository.GetCompetitionAsync(competitionId, ct)
            ?? throw new KeyNotFoundException("Competition was not found.");
        return (await repository.GetAppealsAsync(competitionId, ct)).Select(Map).ToArray();
    }

    public Task<AppealResponse> StartReviewAsync(Guid appealId, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var appeal = await FindAsync(appealId, token);
            await MutableCompetitionAsync(appeal.CompetitionId, token);
            appeal.StartReview();
            await repository.SaveAsync(token);
            return Map(appeal);
        }, ct);

    public Task<AppealResponse> ResolveAsync(
        Guid appealId, ResolveAppealRequest request, Guid userId, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var appeal = await FindAsync(appealId, token);
            await MutableCompetitionAsync(appeal.CompetitionId, token);
            if (request.Accepted is null)
                throw new ArgumentException("The appeal decision is required.");

            if (request.Accepted.Value)
            {
                var candidate = (await repository.GetCandidatesAsync(appeal.CompetitionId, token))
                    .SingleOrDefault(item => item.Application.Id == appeal.ApplicationId);
                if (candidate?.Score is null || candidate.Score.CalculatedAtUtc <= appeal.SubmittedAtUtc
                    || !DocumentReviewSnapshot.AllValid(candidate.Documents)
                    || candidate.Score.DocumentReviewFingerprint != DocumentReviewSnapshot.Fingerprint(candidate.Documents))
                    throw new ApplicationConflictException(
                        "Review documentation and recalculate current points after the appeal before accepting it.");
            }

            appeal.Resolve(request.Accepted.Value, request.Response, userId, clock.GetUtcNow().UtcDateTime);
            await repository.SaveAsync(token);
            return Map(appeal);
        }, ct);

    private async Task<Competition> MutableCompetitionAsync(Guid competitionId, CancellationToken ct)
    {
        var competition = await repository.GetCompetitionAsync(competitionId, ct)
            ?? throw new KeyNotFoundException("Competition was not found.");
        if (competition.Status != CompetitionStatus.Closed)
            throw new ApplicationConflictException("Appeals can only be changed before the competition is finalized.");
        return competition;
    }

    private async Task<StudentApplication> OwnedApplicationAsync(
        Guid applicationId, Guid studentId, CancellationToken ct)
    {
        var application = await repository.GetApplicationAsync(applicationId, ct);
        if (application is null || application.StudentId != studentId)
            throw new KeyNotFoundException("Application was not found.");
        return application;
    }

    private async Task<Appeal> FindAsync(Guid id, CancellationToken ct) =>
        await repository.GetAppealAsync(id, ct) ?? throw new KeyNotFoundException("Appeal was not found.");

    private static AppealResponse Map(Appeal appeal) => new(
        appeal.Id, appeal.ApplicationId, appeal.CompetitionId, appeal.StudentId,
        appeal.Reason,
        appeal.Status == AppealStatus.UnderReview ? "UNDER_REVIEW" : appeal.Status.ToString().ToUpperInvariant(),
        appeal.SubmittedAtUtc, appeal.Response, appeal.ResolvedAtUtc, appeal.ResolvedByUserId);
}
