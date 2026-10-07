using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using StudentCenter.NotificationService.Domain.Entities;

namespace StudentCenter.NotificationService.Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(Notification notification, string address, CancellationToken ct);
}

public sealed class EmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(Notification notification, string address, CancellationToken ct)
    {
        var settings = options.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = notification.Title.Replace('\r', ' ').Replace('\n', ' '),
            Body = $"{notification.Message}\n\nView your notifications: {settings.PortalUrl}\n\nStudent Center",
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(address));
        message.Headers.Add("X-StudentCenter-Notification", notification.Id.ToString());
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrEmpty(settings.UserName)
                ? null : new NetworkCredential(settings.UserName, settings.Password)
        };
        await client.SendMailAsync(message, ct);
    }
}
