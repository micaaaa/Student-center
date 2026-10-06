using StudentCenter.StudentService.Application.DTOs;
using StudentCenter.StudentService.Application.Exceptions;
using StudentCenter.StudentService.Application.Interfaces;
using StudentCenter.StudentService.Domain.Entities;
using StudentCenter.StudentService.Domain.Enums;

namespace StudentCenter.StudentService.Application.Services;

public sealed class StudentProfileService(IStudentRepository studentRepository) : IStudentService
{
    public async Task<StudentSearchResponse> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await studentRepository.SearchAsync(search?.Trim(), page, pageSize, cancellationToken);
        return new StudentSearchResponse(
            items.Select(student => new StudentSummaryResponse(
                student.Id, student.UserId, student.StudentNumber,
                student.FirstName, student.LastName, student.Email,
                student.Status.ToString().ToUpperInvariant())).ToList(),
            totalCount, page, pageSize);
    }
    public async Task<StudentResponse> CreateAsync(Guid userId, CreateStudentProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (await studentRepository.GetByUserIdAsync(userId, cancellationToken) is not null)
            throw new ConflictException("A student profile already exists for this user.");
        var studentNumber = request.StudentNumber.Trim().ToUpperInvariant();
        if (await studentRepository.StudentNumberExistsAsync(studentNumber, cancellationToken))
            throw new ConflictException("Student number is already in use.");
        var student = new Student(
            userId,
            studentNumber,
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim().ToLowerInvariant());
        await studentRepository.AddAsync(student, cancellationToken);
        return ToResponse(student);
    }

    public async Task<StudentResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ToResponse(await GetByUserIdAsync(userId, cancellationToken));

    public async Task<StudentResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ToResponse(
        await studentRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Student was not found."));

    public async Task<StudentSummaryResponse> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Student was not found.");
        return new StudentSummaryResponse(
            student.Id,
            student.UserId,
            student.StudentNumber,
            student.FirstName,
            student.LastName,
            student.Email,
            student.Status.ToString().ToUpperInvariant());
    }

    public async Task<StudentResponse> UpdateMyProfileAsync(Guid userId, UpdateStudentProfileRequest request, CancellationToken cancellationToken = default)
    {
        FundingType? fundingType = null;
        if (!string.IsNullOrWhiteSpace(request.FundingType))
        {
            if (!Enum.TryParse<FundingType>(request.FundingType, true, out var parsedFundingType))
                throw new ConflictException("Funding type is not valid.");
            fundingType = parsedFundingType;
        }

        var student = await GetByUserIdAsync(userId, cancellationToken);
        student.UpdateProfile(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            request.Phone?.Trim(),
            request.Faculty?.Trim(),
            request.StudyProgram?.Trim(),
            request.StudyLevel?.Trim(),
            request.YearOfStudy,
            fundingType,
            request.Address?.Trim());
        await studentRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(student);
    }

    private async Task<Student> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await studentRepository.GetByUserIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Student profile was not found.");

    private static StudentResponse ToResponse(Student student) => new(
        student.Id,
        student.UserId,
        student.StudentNumber,
        student.FirstName,
        student.LastName,
        student.Email,
        student.Phone,
        student.Faculty,
        student.StudyProgram,
        student.StudyLevel,
        student.YearOfStudy,
        student.FundingType?.ToString().ToUpperInvariant(),
        student.Address,
        student.Status.ToString().ToUpperInvariant());
}
