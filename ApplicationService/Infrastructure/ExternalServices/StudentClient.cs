using System.Net;
using System.Net.Http.Json;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Infrastructure.ExternalServices;

public sealed class StudentClient(HttpClient client, IHttpContextAccessor context) : IStudentClient
{
    public async Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/students/me");
        var token = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(token);
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ApplicationConflictException("Create a student profile before managing applications.");
        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<StudentProfile>(cancellationToken: ct);
        if (student is null || student.Id == Guid.Empty)
            throw new HttpRequestException("Student service returned an invalid profile.");
        return student.Id;
    }

    private sealed record StudentProfile(Guid Id);
}
