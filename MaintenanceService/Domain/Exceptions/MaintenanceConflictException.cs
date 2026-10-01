namespace StudentCenter.MaintenanceService.Domain.Exceptions;

public sealed class MaintenanceConflictException(string message) : Exception(message)
{
}
