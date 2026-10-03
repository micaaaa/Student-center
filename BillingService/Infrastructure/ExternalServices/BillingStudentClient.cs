using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using StudentCenter.BillingService.Application.Interfaces;

namespace StudentCenter.BillingService.Infrastructure.ExternalServices;

public sealed class StudentLookupException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class BillingStudentClient(HttpClient client, IHttpContextAccessor context) : IBillingStudentClient
{
    public async Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(context.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || userId == Guid.Empty)
        {
            throw new StudentLookupException(401, "A valid authenticated student identity is required.");
        }

        var profile = await ReadAsync("api/students/me", ct);
        if (profile.UserId != userId)
        {
            throw new StudentLookupException(503, "Student service returned an invalid profile owner.");
        }

        return profile.Id;
    }

    public async Task EnsureExistsAsync(Guid studentId, CancellationToken ct)
    {
        var profile = await ReadAsync($"api/students/{studentId}", ct);
        if (profile.Id != studentId)
        {
            throw new StudentLookupException(503, "Student service returned an invalid profile identifier.");
        }
    }

    private async Task<StudentProfile> ReadAsync(string path, CancellationToken ct)
    {
        if (!AuthenticationHeaderValue.TryParse(context.HttpContext?.Request.Headers.Authorization.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new StudentLookupException(401, "A bearer token is required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = authorization;
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new StudentLookupException(404, "Student profile not found.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new StudentLookupException((int)response.StatusCode, "Student profile access was denied.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new StudentLookupException(503, "Student service is temporarily unavailable.");
            }

            var profile = await response.Content.ReadFromJsonAsync<StudentProfile>(cancellationToken: ct);
            if (profile is null || profile.Id == Guid.Empty || profile.UserId == Guid.Empty)
            {
                throw new StudentLookupException(503, "Student service returned invalid profile data.");
            }

            return profile;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new StudentLookupException(503, "Student service is temporarily unavailable.");
        }
    }

    private sealed record StudentProfile(Guid Id, Guid UserId);
}
