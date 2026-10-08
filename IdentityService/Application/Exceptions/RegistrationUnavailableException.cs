namespace StudentCenter.IdentityService.Application.Exceptions;

public sealed class RegistrationUnavailableException() : Exception(
    "Student registration is temporarily unavailable. Please retry using the same username, email and password.");
