namespace StudentCenter.FoodService.Domain.Exceptions;

public sealed class FoodConflictException(string message) : Exception(message)
{
}
