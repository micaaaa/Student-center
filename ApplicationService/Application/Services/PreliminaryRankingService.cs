using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;
using StudentCenter.ApplicationService.Domain.Services;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class PreliminaryRankingService(IRankingRepository repository, IStudentClient students)
{
    public Task<RankingResponse> GenerateAsync(
        Guid competitionId, RankingTieRule tieRule, Guid userId, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            await EnsureClosedAsync(competitionId, token);
            if (!Enum.IsDefined(tieRule))
                throw new ArgumentException("Unknown tie rule.");

            var existing = await repository.GetPreliminaryAsync(competitionId, token);
            if (existing?.Status == RankingStatus.Published)
                throw new ApplicationConflictException("The preliminary ranking has already been published.");

            var candidates = await GetReadyCandidatesAsync(competitionId, token);
            var ranking = existing ?? new Ranking(competitionId);
            var ordered = candidates.OrderByDescending(candidate => candidate.Score!.TotalPoints)
                .ThenBy(candidate => tieRule == RankingTieRule.EarlierSubmission
                    ? candidate.Application.SubmittedAtUtc : DateTime.MinValue)
                .ThenBy(candidate => candidate.Application.Id)
                .ToArray();

            var entries = new List<RankingEntry>();
            var position = 0;
            for (var index = 0; index < ordered.Length; index++)
            {
                if (tieRule == RankingTieRule.EarlierSubmission || index == 0
                    || ordered[index].Score!.TotalPoints != ordered[index - 1].Score!.TotalPoints)
                    position = index + 1;

                var candidate = ordered[index];
                entries.Add(new RankingEntry(ranking.Id, candidate.Application.Id,
                    candidate.Application.StudentId, position, candidate.Score!.TotalPoints));
            }

            ranking.ReplaceDraft(entries, Fingerprint(candidates), tieRule, userId);
            if (existing is null)
                await repository.AddAsync(ranking, token);
            await repository.SaveAsync(token);
            return Map(ranking);
        }, ct);

    public async Task<RankingResponse> GetForStaffAsync(Guid competitionId, CancellationToken ct) =>
        Map(await FindAsync(competitionId, ct));

    public Task<RankingResponse> PublishAsync(Guid competitionId, Guid userId, CancellationToken ct) =>
        repository.InTransactionAsync(async token =>
        {
            await EnsureClosedAsync(competitionId, token);
            var ranking = await FindAsync(competitionId, token);
            if (ranking.Status != RankingStatus.Draft)
                throw new ApplicationConflictException("The preliminary ranking has already been published.");

            var candidates = await GetReadyCandidatesAsync(competitionId, token);
            if (ranking.SourceFingerprint != Fingerprint(candidates))
                throw new ApplicationConflictException("Ranking data has changed. Generate the draft again.");

            ranking.Publish(userId);
            await repository.SaveAsync(token);
            return Map(ranking);
        }, ct);

    public async Task<PublishedRankingResponse> GetPublishedAsync(Guid competitionId, CancellationToken ct)
    {
        var ranking = await FindAsync(competitionId, ct);
        if (ranking.Status != RankingStatus.Published)
            throw new KeyNotFoundException("Published preliminary ranking was not found.");

        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return new PublishedRankingResponse(
            ranking.Id, ranking.CompetitionId, "PRELIMINARY", ranking.TieRule.ToString(), ranking.PublishedAtUtc,
            OrderedEntries(ranking).Select(entry => new PublishedRankingEntryResponse(
                entry.ApplicationId, entry.Position, entry.TotalPoints, entry.StudentId == studentId)).ToArray());
    }

    private async Task EnsureClosedAsync(Guid competitionId, CancellationToken ct)
    {
        var competition = await repository.GetCompetitionAsync(competitionId, ct)
            ?? throw new KeyNotFoundException("Competition was not found.");
        if (competition.Status != CompetitionStatus.Closed)
            throw new ApplicationConflictException("Close the competition before generating or publishing its ranking.");
    }

    private async Task<Ranking> FindAsync(Guid competitionId, CancellationToken ct) =>
        await repository.GetPreliminaryAsync(competitionId, ct)
        ?? throw new KeyNotFoundException("Preliminary ranking was not found.");

    private async Task<IReadOnlyCollection<RankingCandidate>> GetReadyCandidatesAsync(
        Guid competitionId, CancellationToken ct)
    {
        var candidates = (await repository.GetCandidatesAsync(competitionId, ct))
            .Where(candidate => candidate.Application.Status is not (
                ApplicationStatus.Draft or ApplicationStatus.Withdrawn or ApplicationStatus.Rejected))
            .ToArray();
        if (candidates.Length == 0)
            throw new ApplicationConflictException("There are no active submitted applications to rank.");

        foreach (var candidate in candidates)
        {
            if (candidate.Application.Status != ApplicationStatus.UnderReview
                || candidate.Application.SubmittedAtUtc is null
                || candidate.Score is null
                || !DocumentReviewSnapshot.AllValid(candidate.Documents)
                || candidate.Score.DocumentReviewFingerprint != DocumentReviewSnapshot.Fingerprint(candidate.Documents))
                throw new ApplicationConflictException(
                    $"Application {candidate.Application.Id} requires completed document review and current scoring.");
        }

        return candidates;
    }

    private static string Fingerprint(IEnumerable<RankingCandidate> candidates)
    {
        var snapshot = string.Join("|", candidates.OrderBy(candidate => candidate.Application.Id).Select(candidate =>
            string.Join(":", candidate.Application.Id.ToString("N"),
                candidate.Application.StudentId.ToString("N"),
                ((int)candidate.Application.Status).ToString(CultureInfo.InvariantCulture),
                candidate.Application.SubmittedAtUtc?.Ticks.ToString(CultureInfo.InvariantCulture),
                candidate.Score!.Id.ToString("N"),
                candidate.Score.TotalPoints.ToString(CultureInfo.InvariantCulture),
                candidate.Score.CalculatedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                candidate.Score.DocumentReviewFingerprint)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }

    private static IOrderedEnumerable<RankingEntry> OrderedEntries(Ranking ranking) =>
        ranking.Entries.OrderBy(entry => entry.Position).ThenBy(entry => entry.ApplicationId);

    private static RankingResponse Map(Ranking ranking) => new(
        ranking.Id, ranking.CompetitionId, "PRELIMINARY", ranking.Status.ToString().ToUpperInvariant(),
        ranking.TieRule.ToString(), ranking.GeneratedAtUtc, ranking.PublishedAtUtc,
        OrderedEntries(ranking).Select(entry => new RankingEntryResponse(
            entry.ApplicationId, entry.StudentId, entry.Position, entry.TotalPoints)).ToArray());
}
