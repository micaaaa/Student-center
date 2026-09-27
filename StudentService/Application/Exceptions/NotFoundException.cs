namespace StudentCenter.StudentService.Application.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);
