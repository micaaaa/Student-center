namespace StudentCenter.ApplicationService.Domain.Exceptions;

public sealed class ApplicationConflictException(string message) : Exception(message);
