namespace StudentCenter.ApplicationService.Application.DTOs;
public sealed record ApplicationResponse(Guid Id,Guid CompetitionId,Guid StudentId,string Status,DateTime CreatedAtUtc,DateTime? SubmittedAtUtc,string? Note);
