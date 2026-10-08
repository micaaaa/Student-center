using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Application.Exceptions;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Domain.Entities;

namespace StudentCenter.IdentityService.Infrastructure.ExternalServices;

public sealed class StudentRegistrationClient(HttpClient http, IJwtTokenGenerator tokens) : IStudentRegistrationClient
{
    public async Task CreateProfileAsync(User user, RegisterRequest request, CancellationToken cancellationToken)
    {
        var token = tokens.CreateAccessToken(user);
        var number = request.StudentNumber.Trim().ToUpperInvariant();
        try
        {
            if (await ProfileExistsAsync(user.Id, number, token, cancellationToken))
                return;

            using var message = new HttpRequestMessage(HttpMethod.Post, "api/students/me");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Content = JsonContent.Create(new
            {
                studentNumber = number,
                firstName = request.FirstName.Trim(),
                lastName = request.LastName.Trim(),
                email = user.Email
            });
            using var response = await http.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // A timed-out or concurrent attempt may already have created this profile.
                if (await ProfileExistsAsync(user.Id, number, token, cancellationToken))
                    return;
                throw new ConflictException("Student number is already in use.");
            }
            if (response.StatusCode != HttpStatusCode.Created)
                throw new RegistrationUnavailableException();
        }
        catch (HttpRequestException)
        {
            throw new RegistrationUnavailableException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RegistrationUnavailableException();
        }
        catch (JsonException)
        {
            throw new RegistrationUnavailableException();
        }
    }

    private async Task<bool> ProfileExistsAsync(Guid userId, string number, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/students/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (!response.IsSuccessStatusCode)
            throw new RegistrationUnavailableException();
        var profile = await response.Content.ReadFromJsonAsync<RegisteredProfile>(ct);
        if (profile is null || profile.UserId != userId)
            throw new RegistrationUnavailableException();
        if (profile.StudentNumber != number)
            throw new ConflictException("Registration already created a profile with a different student number. Retry with the original student number.");
        return true;
    }

    private sealed record RegisteredProfile(Guid UserId, string StudentNumber);
}
