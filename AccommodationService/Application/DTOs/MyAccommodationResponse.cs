namespace StudentCenter.AccommodationService.Application.DTOs;

public sealed record MyAccommodationResponse(
    Guid Id, string AcademicYear, string Status, DateTime AssignedAtUtc,
    DateTime? MovedInAtUtc, DateTime? MovedOutAtUtc, DateTime? CancelledAtUtc,
    StudentDormResponse Dorm, StudentRoomResponse Room);

public sealed record StudentDormResponse(Guid Id, string Name, string Address, string City);
public sealed record StudentRoomResponse(Guid Id, string Number, int Floor);
