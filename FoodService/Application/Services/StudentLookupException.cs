namespace StudentCenter.FoodService.Application.Services;

public sealed class StudentLookupException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
