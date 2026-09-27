using StudentCenter.StudentService.Application.DTOs;

namespace StudentCenter.StudentService.Application.Interfaces;

public interface IStudentService
{
    Task<StudentResponse> CreateAsync(Guid userId, CreateStudentProfileRequest request, CancellationToken cancellationToken = default);
    Task<StudentResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<StudentResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StudentSummaryResponse> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StudentResponse> UpdateMyProfileAsync(Guid userId, UpdateStudentProfileRequest request, CancellationToken cancellationToken = default);
}
