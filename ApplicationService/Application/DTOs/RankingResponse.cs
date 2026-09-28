using System.ComponentModel.DataAnnotations;
using StudentCenter.ApplicationService.Domain.Enums;

namespace StudentCenter.ApplicationService.Application.DTOs;

public sealed class GenerateRankingRequest
{
    [EnumDataType(typeof(RankingTieRule))]
    public RankingTieRule TieRule { get; init; } = RankingTieRule.SharedPosition;
}

public sealed record RankingEntryResponse(
    Guid ApplicationId, Guid StudentId, int Position, decimal TotalPoints);

public sealed record RankingResponse(
    Guid Id,
    Guid CompetitionId,
    string Type,
    string Status,
    string TieRule,
    DateTime GeneratedAtUtc,
    DateTime? PublishedAtUtc,
    IReadOnlyCollection<RankingEntryResponse> Entries);

public sealed record PublishedRankingEntryResponse(
    Guid ApplicationId, int Position, decimal TotalPoints, bool IsMine);

public sealed record PublishedRankingResponse(
    Guid Id,
    Guid CompetitionId,
    string Type,
    string TieRule,
    DateTime? PublishedAtUtc,
    IReadOnlyCollection<PublishedRankingEntryResponse> Entries);
