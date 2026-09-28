using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class CompetitionConclusionService(
    IConclusionRepository repository, IStudentClient students, TimeProvider clock)
{
    public Task<ConclusionSettingsResponse> ConfigureAsync(
        Guid competitionId, ConclusionSettingsRequest request, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var competition = await FindCompetitionAsync(competitionId, token);
            if (request.AvailablePlaces is null || request.AppealDeadlineUtc is null)
                throw new ArgumentException("Available places and the appeal deadline are required.");
            await PublishedPreliminaryAsync(competitionId, token);
            competition.ConfigureConclusion(
                request.AvailablePlaces.Value, request.AppealDeadlineUtc.Value, clock.GetUtcNow().UtcDateTime);
            await repository.SaveAsync(token);
            return Settings(competition);
        }, ct);

    public async Task<ConclusionSettingsResponse> GetSettingsAsync(Guid competitionId, CancellationToken ct) =>
        Settings(await FindCompetitionAsync(competitionId, ct));

    public Task<FinalRankingResponse> GenerateAsync(Guid competitionId, Guid userId, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var competition = await ReadyCompetitionAsync(competitionId, token);
            var preliminary = await PublishedPreliminaryAsync(competitionId, token);
            var existing = await repository.GetFinalAsync(competitionId, token);
            if (existing?.Status == RankingStatus.Published)
                throw new ApplicationConflictException("The final ranking has already been published.");

            var candidates = await ReadyCandidatesAsync(competitionId, token);
            var ranking = existing ?? new Ranking(competitionId, RankingType.Final);
            var entries = PreliminaryRankingService.BuildEntries(ranking, candidates, preliminary.TieRule);
            EnsureCapacityBoundary(entries, competition.AvailablePlaces!.Value, preliminary.TieRule);

            ranking.ReplaceDraft(entries, SourceFingerprint(candidates, competition), preliminary.TieRule, userId);
            ranking.SetCapacity(competition.AvailablePlaces.Value);
            if (existing is null)
                await repository.AddAsync(ranking, token);
            await repository.SaveAsync(token);
            return Map(ranking);
        }, ct);

    public async Task<FinalRankingResponse> GetForStaffAsync(Guid competitionId, CancellationToken ct) =>
        Map(await FindFinalAsync(competitionId, ct));

    public Task<FinalRankingResponse> PublishAsync(Guid competitionId, Guid userId, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            var competition = await ReadyCompetitionAsync(competitionId, token);
            var ranking = await FindFinalAsync(competitionId, token);
            if (ranking.Status != RankingStatus.Draft)
                throw new ApplicationConflictException("The final ranking has already been published.");
            var candidates = await ReadyCandidatesAsync(competitionId, token);
            if (ranking.SourceFingerprint != SourceFingerprint(candidates, competition))
                throw new ApplicationConflictException("Final ranking data changed. Generate the draft again.");
            EnsureCapacityBoundary(ranking.Entries, competition.AvailablePlaces!.Value, ranking.TieRule);

            var applications = (await repository.GetApplicationsAsync(competitionId, token))
                .ToDictionary(application => application.Id);
            var now = clock.GetUtcNow().UtcDateTime;
            ranking.Publish(userId);
            foreach (var entry in ranking.Entries)
            {
                var eligible = entry.Position <= competition.AvailablePlaces.Value;
                applications[entry.ApplicationId].Decide(eligible);
                var decision = new AccommodationEligibility(
                    competition.Id, entry.ApplicationId, entry.StudentId, ranking.Id,
                    competition.AcademicYear, eligible, now);
                await repository.AddDecisionAsync(decision, token);
                if (eligible)
                    await repository.AddEventAsync(OutboxMessage.EligibilityGranted(decision), token);
            }
            competition.FinalizeCompetition(now);
            await repository.SaveAsync(token);
            return Map(ranking);
        }, ct);

    public async Task<PublishedFinalRankingResponse> GetPublishedAsync(Guid competitionId, CancellationToken ct)
    {
        var ranking = await FindFinalAsync(competitionId, ct);
        if (ranking.Status != RankingStatus.Published)
            throw new KeyNotFoundException("Published final ranking was not found.");
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return new PublishedFinalRankingResponse(
            ranking.Id, ranking.CompetitionId, ranking.TieRule.ToString(),
            ranking.AvailablePlaces!.Value, ranking.PublishedAtUtc,
            ranking.Entries.OrderBy(entry => entry.Position).ThenBy(entry => entry.ApplicationId)
                .Select(entry => new PublishedFinalEntryResponse(
                    entry.ApplicationId, entry.Position, entry.TotalPoints,
                    entry.Position <= ranking.AvailablePlaces.Value, entry.StudentId == studentId)).ToArray());
    }

    public async Task<EligibilityResponse> GetMyEligibilityAsync(Guid competitionId, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        var decision = (await repository.GetDecisionsAsync(competitionId, ct))
            .SingleOrDefault(item => item.StudentId == studentId)
            ?? throw new KeyNotFoundException("Accommodation eligibility was not found.");
        return MapDecision(decision);
    }

    public async Task<IReadOnlyCollection<EligibilityResponse>> GetDecisionsAsync(Guid competitionId, CancellationToken ct)
    {
        await FindCompetitionAsync(competitionId, ct);
        return (await repository.GetDecisionsAsync(competitionId, ct)).Select(MapDecision).ToArray();
    }

    private async Task<Competition> ReadyCompetitionAsync(Guid competitionId, CancellationToken ct)
    {
        var competition = await FindCompetitionAsync(competitionId, ct);
        if (competition.Status != CompetitionStatus.Closed || competition.AvailablePlaces is null
            || competition.AppealDeadlineUtc is null || clock.GetUtcNow().UtcDateTime <= competition.AppealDeadlineUtc)
            throw new ApplicationConflictException(
                "Configure the closed competition and wait for its appeal deadline before final ranking.");
        await PublishedPreliminaryAsync(competitionId, ct);
        if ((await repository.GetAppealsAsync(competitionId, ct))
            .Any(appeal => appeal.Status is AppealStatus.Submitted or AppealStatus.UnderReview))
            throw new ApplicationConflictException("All appeals must be resolved before final ranking.");
        return competition;
    }

    private Task<IReadOnlyCollection<RankingCandidate>> ReadyCandidatesAsync(Guid competitionId, CancellationToken ct) =>
        new PreliminaryRankingService(repository, students).GetReadyCandidatesAsync(competitionId, ct);

    private async Task<Competition> FindCompetitionAsync(Guid id, CancellationToken ct) =>
        await repository.GetCompetitionAsync(id, ct) ?? throw new KeyNotFoundException("Competition was not found.");

    private async Task<Ranking> FindFinalAsync(Guid competitionId, CancellationToken ct) =>
        await repository.GetFinalAsync(competitionId, ct) ?? throw new KeyNotFoundException("Final ranking was not found.");

    private async Task<Ranking> PublishedPreliminaryAsync(Guid competitionId, CancellationToken ct)
    {
        var ranking = await repository.GetPreliminaryAsync(competitionId, ct);
        if (ranking?.Status != RankingStatus.Published)
            throw new ApplicationConflictException("Publish a preliminary ranking first.");
        return ranking;
    }

    private static void EnsureCapacityBoundary(
        IReadOnlyCollection<RankingEntry> entries, int places, RankingTieRule tieRule)
    {
        if (tieRule != RankingTieRule.SharedPosition)
            return;
        var ordered = entries.OrderBy(entry => entry.Position).ToArray();
        if (places > 0 && places < ordered.Length && ordered[places - 1].Position == ordered[places].Position)
            throw new ApplicationConflictException(
                "A tied group crosses the capacity boundary. Adjust capacity to include or exclude the entire group.");
    }

    private static string SourceFingerprint(IEnumerable<RankingCandidate> candidates, Competition competition) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(":", PreliminaryRankingService.Fingerprint(candidates),
            competition.AvailablePlaces!.Value.ToString(CultureInfo.InvariantCulture),
            competition.AppealDeadlineUtc!.Value.Ticks.ToString(CultureInfo.InvariantCulture)))));

    private static FinalRankingResponse Map(Ranking ranking) => new(
        ranking.Id, ranking.CompetitionId, ranking.Status.ToString().ToUpperInvariant(), ranking.TieRule.ToString(),
        ranking.AvailablePlaces!.Value, ranking.PublishedAtUtc,
        ranking.Entries.OrderBy(entry => entry.Position).ThenBy(entry => entry.ApplicationId)
            .Select(entry => new FinalRankingEntryResponse(
                entry.ApplicationId, entry.StudentId, entry.Position, entry.TotalPoints,
                entry.Position <= ranking.AvailablePlaces.Value)).ToArray());

    private static ConclusionSettingsResponse Settings(Competition competition) => new(
        competition.Id, competition.AvailablePlaces, competition.AppealDeadlineUtc,
        competition.Status.ToString().ToUpperInvariant());

    private static EligibilityResponse MapDecision(AccommodationEligibility decision) => new(
        decision.Id, decision.CompetitionId, decision.ApplicationId, decision.StudentId,
        decision.AcademicYear, decision.Eligible, decision.DecisionDateUtc, decision.RankingId);
}
