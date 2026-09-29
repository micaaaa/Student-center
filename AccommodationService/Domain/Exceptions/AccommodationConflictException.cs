namespace StudentCenter.AccommodationService.Domain.Exceptions;

public sealed class AccommodationConflictException(string message) : Exception(message);
