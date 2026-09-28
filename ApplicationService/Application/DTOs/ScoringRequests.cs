using System.ComponentModel.DataAnnotations;

namespace StudentCenter.ApplicationService.Application.DTOs;

public sealed class CalculateScoreRequest
{
    [Required]
    public decimal? AcademicPoints { get; init; }

    [Required]
    public decimal? IncomePoints { get; init; }

    [Required]
    public decimal? ECTSPoints { get; init; }

    [Required]
    public decimal? StudyYearPoints { get; init; }

    [Required]
    public decimal? AdditionalPoints { get; init; }
}

public sealed record ScoringResponse(
    Guid Id,
    Guid ApplicationId,
    decimal AcademicPoints,
    decimal IncomePoints,
    decimal ECTSPoints,
    decimal StudyYearPoints,
    decimal AdditionalPoints,
    decimal TotalPoints,
    DateTime CalculatedAtUtc,
    Guid CalculatedByUserId,
    bool IsCurrent);
