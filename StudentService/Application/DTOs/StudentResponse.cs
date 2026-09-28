namespace StudentCenter.StudentService.Application.DTOs;

public sealed record StudentResponse(
    Guid Id,
    Guid UserId,
    string StudentNumber,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Faculty,
    string? StudyProgram,
    string? StudyLevel,
    int? YearOfStudy,
    string? FundingType,
    string? Address,
    string Status);
