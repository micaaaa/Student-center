using System.Net.Http.Headers;

namespace StudentCenter.StudentService.Infrastructure.ExternalServices;

public sealed class StudentAccountDirectory(HttpClient http, IHttpContextAccessor context)
{
    public async Task<Guid[]> GetStudentAccountsAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/student-accounts");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(
            context.HttpContext!.Request.Headers.Authorization.ToString());
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid[]>(ct)
            ?? throw new HttpRequestException("Account directory is unavailable.");
    }
}
