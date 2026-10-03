namespace StudentCenter.BillingService.Domain.Exceptions;

public sealed class BillingConflictException(string message) : Exception(message);
