using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using StudentCenter.MaintenanceService.Application.DTOs;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Exceptions;

namespace StudentCenter.MaintenanceService.Infrastructure.ExternalServices;

public sealed class CurrentStudentContext(IHttpClientFactory clients, IHttpContextAccessor context) : ICurrentStudentContext
{
    public async Task<Guid> GetStudentIdAsync(CancellationToken ct)
    {
        var profile = await ReadAsync<StudentProfile>("StudentService", "api/students/me", ct);
        if (profile.Id == Guid.Empty)
        {
            throw new ServiceLookupException(503, "Student service returned an invalid profile.");
        }

        return profile.Id;
    }

    public async Task<CurrentAccommodation> GetActiveAccommodationAsync(CancellationToken ct)
    {
        var accommodation = await ReadAsync<AccommodationProfile>("AccommodationService", "api/accommodations/me", ct);
        if (accommodation.Id == Guid.Empty || accommodation.Room is null || accommodation.Room.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(accommodation.Status))
        {
            throw new ServiceLookupException(503, "Accommodation service returned invalid accommodation data.");
        }

        if (!string.Equals(accommodation.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new MaintenanceConflictException("You must be moved into an active accommodation to report a fault.");
        }

        return new CurrentAccommodation(accommodation.Id, accommodation.Room.Id);
    }

    private async Task<T> ReadAsync<T>(string service, string path, CancellationToken ct) where T : class
    {
        var header = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new ServiceLookupException(401, "A bearer token is required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = authorization;
        try
        {
            using var client = clients.CreateClient(service);
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ServiceLookupException(404, service == "StudentService"
                    ? "Student profile not found." : "Current accommodation not found.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ServiceLookupException((int)response.StatusCode, "Access to current student data was denied.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ServiceLookupException(503, $"{service} is temporarily unavailable.");
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                ?? throw new ServiceLookupException(503, $"{service} returned invalid data.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ServiceLookupException(503, $"{service} is temporarily unavailable.");
        }
    }

    private sealed record StudentProfile(Guid Id);
    private sealed record AccommodationProfile(Guid Id, string? Status, RoomProfile? Room);
    private sealed record RoomProfile(Guid Id);
}
