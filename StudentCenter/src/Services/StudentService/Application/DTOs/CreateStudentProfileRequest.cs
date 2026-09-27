using System.ComponentModel.DataAnnotations;

namespace StudentCenter.StudentService.Application.DTOs;

public sealed class CreateStudentProfileRequest
{
    [Required, MaxLength(30)] public string StudentNumber { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string FirstName { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; init; } = string.Empty;
}
