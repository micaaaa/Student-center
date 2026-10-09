using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StudentCenter.Security;

public static class AccountAccessValidation
{
    public static JwtBearerEvents Events() => new()
    {
        OnTokenValidated = ValidateAsync,
        OnChallenge = context =>
        {
            if (context.HttpContext.Items.ContainsKey("IdentityUnavailable"))
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            return Task.CompletedTask;
        }
    };

    private static async Task ValidateAsync(TokenValidatedContext context)
    {
        var deletion = context.Principal?.FindFirst("account_anonymization")?.Value;
        if (Guid.TryParse(deletion, out var target)
            && HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path == $"/internal/accounts/{target}/anonymize")
            return;

        var services = context.HttpContext.RequestServices;
        var configuration = services.GetRequiredService<IConfiguration>();
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var url = configuration["Services:IdentityServiceUrl"] ?? "https://localhost:49586/";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(url.TrimEnd('/') + "/"), "api/auth/me"));
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization.ToString(), out var header))
        {
            context.Fail("Invalid session.");
            return;
        }
        request.Headers.Authorization = header;
        try
        {
            using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                context.Fail("Account access has changed.");
                return;
            }
            response.EnsureSuccessStatusCode();
            var account = await response.Content.ReadFromJsonAsync<Account>(context.HttpContext.RequestAborted);
            var permissions = context.Principal!.FindAll("permission").Select(claim => claim.Value).ToHashSet();
            if (account is null || account.Status != "ACTIVE"
                || account.Role != context.Principal.FindFirst(ClaimTypes.Role)?.Value
                || account.Id.ToString() != context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                || !permissions.SetEquals(account.Permissions))
                context.Fail("Account access has changed.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.HttpContext.Items["IdentityUnavailable"] = true;
            context.Fail("Account validation is unavailable.");
        }
    }

    private sealed record Account(Guid Id, string Role, string Status, string[] Permissions);
}
