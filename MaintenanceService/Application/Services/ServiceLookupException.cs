namespace StudentCenter.MaintenanceService.Application.Services;

public sealed class ServiceLookupException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
