using System.ComponentModel.DataAnnotations;

namespace StudentCenter.StudentService.Application.DTOs;

public sealed class CreateStudentProfileRequest : UpdateStudentProfileRequest
{
    [Required, MaxLength(30)]
    public string StudentNumber { get; init; } = string.Empty;

}
