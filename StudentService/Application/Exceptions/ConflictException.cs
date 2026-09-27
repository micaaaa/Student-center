namespace StudentCenter.StudentService.Application.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
