using System.Net.Mail;

namespace StudentCenter.NotificationService.Infrastructure.Email;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Student Center";
    public string PortalUrl { get; set; } = "http://localhost:5173/notifications";

    public bool IsValid() => !Enabled || (
        !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535
        && MailAddress.TryCreate(FromAddress, out _)
        && !FromName.Contains('\r') && !FromName.Contains('\n')
        && (string.IsNullOrEmpty(UserName) == string.IsNullOrEmpty(Password))
        && (EnableSsl || Host is "localhost" or "127.0.0.1" or "::1")
        && Uri.TryCreate(PortalUrl, UriKind.Absolute, out var portal)
        && (portal.Scheme == "https" || portal.Scheme == "http" && portal.IsLoopback));
}
