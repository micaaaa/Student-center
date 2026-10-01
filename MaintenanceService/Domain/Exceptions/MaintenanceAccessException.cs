namespace StudentCenter.MaintenanceService.Domain.Exceptions;

public sealed class MaintenanceAccessException(string message) : Exception(message)
{
}
