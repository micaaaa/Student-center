using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StudentCenter.IdentityService.Application.Exceptions;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Infrastructure.Persistence;
using StudentCenter.IdentityService.Infrastructure.Security;

namespace StudentCenter.IdentityService.Application.Services;

public sealed class AccountDeletionService(
    IdentityDbContext db, IUserRepository users, IHttpClientFactory clients,
    IConfiguration configuration, IOptions<JwtSettings> jwt)
{
    public async Task DeleteAsync(Guid id, string username, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("User was not found.");
        if (username != user.Username)
            throw new ConflictException("Enter the exact username to confirm deletion.");
        user.BeginDeletion();
        await users.SaveChangesAsync(ct);
        await db.RefreshTokens.Where(token => token.UserId == id && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAtUtc, DateTime.UtcNow), ct);

        var settings = jwt.Value;
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim("account_anonymization", id.ToString())],
            expires: DateTime.UtcNow.AddMinutes(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        var bearer = new JwtSecurityTokenHandler().WriteToken(token);
        using var client = clients.CreateClient("AccountLifecycle");
        foreach (var (service, fallback) in new[]
        {
            ("StudentService", "https://localhost:49686/"),
            ("MaintenanceService", "https://localhost:50086/")
        })
        {
            var baseUrl = configuration[$"Services:{service}Url"] ?? fallback;
            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), $"internal/accounts/{id}/anonymize"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            try
            {
                using var response = await client.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception exception) when (exception is HttpRequestException
                || exception is TaskCanceledException && !ct.IsCancellationRequested)
            {
                throw new ConflictException("Account access has been disabled. A linked service is unavailable; retry deletion to finish removing personal details.");
            }
        }
        user.Anonymize();
        await users.SaveChangesAsync(ct);
    }
}
