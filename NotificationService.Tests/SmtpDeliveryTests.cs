using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using StudentCenter.NotificationService.Domain.Entities;
using StudentCenter.NotificationService.Infrastructure.Email;

namespace StudentCenter.NotificationService.Tests;

[TestFixture]
public sealed class SmtpDeliveryTests
{
    [Test]
    public async Task SenderDeliversToLocalSmtpServer()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = ReceiveAsync(listener, timeout.Token);
        var notification = new Notification(Guid.NewGuid(), "ChargeCreated", "STUDENT", Guid.NewGuid(),
            "New charge", "A charge was recorded.", "Charge", Guid.NewGuid(), DateTime.UtcNow);
        var sender = new EmailSender(Options.Create(new EmailOptions
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            EnableSsl = false,
            FromAddress = "notifications@example.com"
        }));

        await sender.SendAsync(notification, "student@example.com", timeout.Token);
        var message = await received;
        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("student@example.com"));
            Assert.That(message, Does.Contain("New charge"));
            Assert.That(message, Does.Contain(notification.Id.ToString()));
        });
    }

    private static async Task<string> ReceiveAsync(TcpListener listener, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\r\n"
        };
        await writer.WriteLineAsync("220 localhost test SMTP");
        var content = new StringBuilder();
        var readingBody = false;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (readingBody)
            {
                if (line == ".")
                {
                    await writer.WriteLineAsync("250 Message accepted");
                    readingBody = false;
                }
                else
                {
                    content.AppendLine(line);
                }
            }
            else if (line == "DATA")
            {
                readingBody = true;
                await writer.WriteLineAsync("354 Send message");
            }
            else if (line == "QUIT")
            {
                await writer.WriteLineAsync("221 Goodbye");
                break;
            }
            else
            {
                await writer.WriteLineAsync("250 OK");
            }
        }
        return content.ToString();
    }
}
