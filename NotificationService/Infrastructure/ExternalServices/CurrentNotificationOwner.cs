using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using StudentCenter.NotificationService.Application.DTOs;
using StudentCenter.NotificationService.Application.Interfaces;

namespace StudentCenter.NotificationService.Infrastructure.ExternalServices;

public sealed class OwnerLookupException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class CurrentNotificationOwner(HttpClient client, IHttpContextAccessor context) : ICurrentNotificationOwner
{
    public async Task<NotificationOwner> GetAsync(CancellationToken ct)
    {
        var http = context.HttpContext;
        if (!Guid.TryParse(http?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty)
        {
            throw new OwnerLookupException(401, "A valid authenticated identity is required.");
        }

        if (!http!.User.IsInRole("STUDENT"))
        {
            return new NotificationOwner(userId, null);
        }

        if (!AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new OwnerLookupException(401, "A bearer token is required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/students/me");
        request.Headers.Authorization = authorization;
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new NotificationOwner(userId, null);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new OwnerLookupException((int)response.StatusCode, "Student profile access was denied.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new OwnerLookupException(503, "Student service is temporarily unavailable.");
            }

            var profile = await response.Content.ReadFromJsonAsync<StudentProfile>(cancellationToken: ct);
            if (profile is null || profile.Id == Guid.Empty || profile.UserId != userId)
            {
                throw new OwnerLookupException(503, "Student service returned an invalid profile.");
            }

            return new NotificationOwner(userId, profile.Id);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new OwnerLookupException(503, "Student service is temporarily unavailable.");
        }
    }

    private sealed record StudentProfile(Guid Id, Guid UserId);
}
