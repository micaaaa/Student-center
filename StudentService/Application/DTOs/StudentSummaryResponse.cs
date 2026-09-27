namespace StudentCenter.StudentService.Application.DTOs;

public sealed record StudentSummaryResponse(
    Guid Id,
    Guid UserId,
    string StudentNumber,
    string FirstName,
    string LastName,
    string Email,
    string Status);
