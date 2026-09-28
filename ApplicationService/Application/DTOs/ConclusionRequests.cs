using System.ComponentModel.DataAnnotations;
using StudentCenter.ApplicationService.Domain.Enums;

namespace StudentCenter.ApplicationService.Application.DTOs;

public sealed class ConclusionSettingsRequest
{
    [Required, Range(0, int.MaxValue)]
    public int? AvailablePlaces { get; init; }

    [Required]
    public DateTime? AppealDeadlineUtc { get; init; }
}

public sealed class SubmitAppealRequest
{
    [Required, MaxLength(4000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class ResolveAppealRequest
{
    [Required]
    public bool? Accepted { get; init; }

    [Required, MaxLength(4000)]
    public string Response { get; init; } = string.Empty;
}

public sealed record AppealResponse(
    Guid Id, Guid ApplicationId, Guid CompetitionId, Guid StudentId,
    string Reason, string Status, DateTime SubmittedAtUtc,
    string? Response, DateTime? ResolvedAtUtc, Guid? ResolvedByUserId);

public sealed record ConclusionSettingsResponse(
    Guid CompetitionId, int? AvailablePlaces, DateTime? AppealDeadlineUtc, string Status);

public sealed record EligibilityResponse(
    Guid Id, Guid CompetitionId, Guid ApplicationId, Guid StudentId,
    string AcademicYear, bool Eligible, DateTime DecisionDateUtc, Guid RankingId);

public sealed record FinalRankingEntryResponse(
    Guid ApplicationId, Guid StudentId, int Position, decimal TotalPoints, bool Eligible);

public sealed record FinalRankingResponse(
    Guid Id, Guid CompetitionId, string Status, string TieRule, int AvailablePlaces,
    DateTime? PublishedAtUtc, IReadOnlyCollection<FinalRankingEntryResponse> Entries);

public sealed record PublishedFinalEntryResponse(
    Guid ApplicationId, int Position, decimal TotalPoints, bool Eligible, bool IsMine);

public sealed record PublishedFinalRankingResponse(
    Guid Id, Guid CompetitionId, string TieRule, int AvailablePlaces,
    DateTime? PublishedAtUtc, IReadOnlyCollection<PublishedFinalEntryResponse> Entries);
