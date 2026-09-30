using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using StudentCenter.FoodService.Application.Interfaces;
using StudentCenter.FoodService.Application.Services;
using StudentCenter.FoodService.Domain.Exceptions;

namespace StudentCenter.FoodService.Infrastructure.ExternalServices;

public sealed class FoodStudentClient(HttpClient client, IHttpContextAccessor context) : IFoodStudentClient
{
    public async Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct)
    {
        var profile = await ReadAsync("api/students/me", ct);
        return profile.Id;
    }

    public async Task EnsureActiveStudentAsync(Guid studentId, CancellationToken ct)
    {
        var profile = await ReadAsync($"api/students/{studentId}/summary", ct);
        if (profile.Id != studentId || string.IsNullOrWhiteSpace(profile.Status))
        {
            throw new StudentLookupException(503, "Student service returned an invalid profile.");
        }

        if (!string.Equals(profile.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new FoodConflictException("Meal entitlement can only be granted to an active student.");
        }
    }

    private async Task<StudentProfile> ReadAsync(string path, CancellationToken ct)
    {
        var header = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
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
            if (profile is null || profile.Id == Guid.Empty)
            {
                throw new StudentLookupException(503, "Student service returned an invalid profile.");
            }

            return profile;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new StudentLookupException(503, "Student service is temporarily unavailable.");
        }
    }

    private sealed record StudentProfile(Guid Id, string? Status);
}
