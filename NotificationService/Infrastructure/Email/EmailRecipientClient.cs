using System.Net.Mail;
using StudentCenter.NotificationService.Domain.Entities;

namespace StudentCenter.NotificationService.Infrastructure.Email;

public interface IEmailRecipientClient
{
    Task<string> GetAsync(Notification notification, CancellationToken ct);
}

public sealed class EmailRecipientClient(HttpClient client, IConfiguration configuration) : IEmailRecipientClient
{
    public async Task<string> GetAsync(Notification notification, CancellationToken ct)
    {
        var service = notification.RecipientKind == "STUDENT" ? "StudentServiceUrl" : "IdentityServiceUrl";
        var baseUri = new Uri(configuration[$"Services:{service}"]!);
        if (baseUri.Scheme != "https")
        {
            throw new InvalidOperationException("Recipient services must use HTTPS.");
        }
        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(baseUri, $"internal/notification-recipients/{notification.RecipientId}"));
        request.Headers.Add("X-Notification-Key", configuration["InternalServices:NotificationKey"]);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var recipient = await response.Content.ReadFromJsonAsync<Recipient>(cancellationToken: ct);
        if (recipient is null || !MailAddress.TryCreate(recipient.Email, out var address))
        {
            throw new InvalidOperationException("Recipient email is not available.");
        }
        return address.Address;
    }

    private sealed record Recipient(string Email);
}
