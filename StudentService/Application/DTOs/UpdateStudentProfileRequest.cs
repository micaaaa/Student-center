using System.ComponentModel.DataAnnotations;

namespace StudentCenter.StudentService.Application.DTOs;

public class UpdateStudentProfileRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Phone, MaxLength(30)]
    public string? Phone { get; init; }

    [MaxLength(200)]
    public string? Faculty { get; init; }

    [MaxLength(200)]
    public string? StudyProgram { get; init; }

    [MaxLength(100)]
    public string? StudyLevel { get; init; }

    [Range(1, 10)]
    public int? YearOfStudy { get; init; }
    public string? FundingType { get; init; }

    [MaxLength(500)]
    public string? Address { get; init; }
}
