using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Application.Services;

namespace StudentCenter.AccommodationService.Infrastructure.ExternalServices;

public sealed class CurrentStudentClient(HttpClient client, IHttpContextAccessor context) : ICurrentStudentClient
{
    public async Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct)
    {
        var header = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
            throw new StudentLookupException(401, "A bearer token is required.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/students/me");
        request.Headers.Authorization = authorization;
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new StudentLookupException(404, "Create a student profile before viewing accommodation.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new StudentLookupException((int)response.StatusCode, "Student profile access was denied.");
            if (!response.IsSuccessStatusCode)
                throw new StudentLookupException(503, "Student service is temporarily unavailable.");

            var profile = await response.Content.ReadFromJsonAsync<StudentProfile>(cancellationToken: ct);
            if (profile is null || profile.Id == Guid.Empty)
                throw new StudentLookupException(503, "Student service returned an invalid profile.");
            return profile.Id;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new StudentLookupException(503, "Student service is temporarily unavailable.");
        }
    }

    private sealed record StudentProfile(Guid Id);
}
