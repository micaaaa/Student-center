using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.Infrastructure.ExternalServices;

public sealed class StaffDirectoryClient(HttpClient client, IHttpContextAccessor context) : IStaffDirectoryClient
{
    public async Task EnsureActiveStaffAsync(Guid userId, CancellationToken ct)
    {
        var header = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new ServiceLookupException(401, "A bearer token is required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/staff-directory/{userId}");
        request.Headers.Authorization = authorization;
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ServiceLookupException(404, "Staff account not found.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ServiceLookupException((int)response.StatusCode, "Staff directory access was denied.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ServiceLookupException(503, "Identity service is temporarily unavailable.");
            }

            var user = await response.Content.ReadFromJsonAsync<StaffAccount>(cancellationToken: ct);
            if (user is null || user.Id != userId || string.IsNullOrWhiteSpace(user.Status)
                || user.Role is not ("STAFF" or "ADMIN"))
            {
                throw new ServiceLookupException(503, "Identity service returned invalid staff data.");
            }

            if (user.Status != "ACTIVE")
            {
                throw new MaintenanceConflictException("Only an active staff account can be registered as a worker.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ServiceLookupException(503, "Identity service is temporarily unavailable.");
        }
    }

    private sealed record StaffAccount(Guid Id, string Role, string Status);
}
