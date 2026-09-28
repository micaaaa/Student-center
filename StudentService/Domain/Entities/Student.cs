using StudentCenter.StudentService.Domain.Enums;

namespace StudentCenter.StudentService.Domain.Entities;

public sealed class Student
{
    private Student()
    {
    }

    public Student(Guid userId, string studentNumber, string firstName, string lastName, string email)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        StudentNumber = studentNumber;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Status = StudentStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string StudentNumber { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? Faculty { get; private set; }
    public string? StudyProgram { get; private set; }
    public string? StudyLevel { get; private set; }
    public int? YearOfStudy { get; private set; }
    public FundingType? FundingType { get; private set; }
    public string? Address { get; private set; }
    public StudentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void UpdateProfile(
        string firstName,
        string lastName,
        string email,
        string? phone,
        string? faculty,
        string? studyProgram,
        string? studyLevel,
        int? yearOfStudy,
        FundingType? fundingType,
        string? address)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        Faculty = faculty;
        StudyProgram = studyProgram;
        StudyLevel = studyLevel;
        YearOfStudy = yearOfStudy;
        FundingType = fundingType;
        Address = address;
    }
}
