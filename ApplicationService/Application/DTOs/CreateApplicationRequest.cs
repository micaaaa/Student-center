namespace StudentCenter.ApplicationService.Application.DTOs;
public sealed record CreateApplicationRequest(Guid CompetitionId,string? Note=null);
