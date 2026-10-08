using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using StudentCenter.NotificationService.Domain.Entities;
using StudentCenter.NotificationService.Infrastructure.Email;
using StudentCenter.Security;

namespace StudentCenter.NotificationService.Tests;

[TestFixture]
public sealed class EmailTests
{

    [Test]
    public void RetryDelayGrowsAndIsBounded()
    {
        Assert.That(Enumerable.Range(1, 8).Select(attempt => EmailDelivery.RetryDelay(attempt).TotalMinutes),
            Is.EqualTo(new double[] { 1, 2, 4, 8, 16, 32, 60, 60 }));
    }

    [TestCase(null, false)]
    [TestCase("wrong-key", false)]
    [TestCase("test-only-notification-key-with-32-characters", true)]
    public void RecipientEndpointRequiresItsOwnKey(string? supplied, bool allowed)
    {
        var configuration = Configuration();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        if (supplied is not null)
        {
            context.Request.Headers["X-Notification-Key"] = supplied;
        }
        context.Request.Headers.Authorization = "Bearer ordinary-user-token";
        var filter = new AuthorizationFilterContext(
            new ActionContext(context, new RouteData(), new ActionDescriptor()), []);
        new NotificationServiceKeyAttribute().OnAuthorization(filter);
        Assert.That(filter.Result is null, Is.EqualTo(allowed));
    }

    [TestCase("STUDENT", "https://students.test/")]
    [TestCase("USER", "https://identity.test/")]
    public async Task RecipientLookupUsesCorrectServiceAndCredential(string kind, string service)
    {
        var notification = CreateNotification(kind);
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.That(request.RequestUri!.ToString(),
                Is.EqualTo($"{service}internal/notification-recipients/{notification.RecipientId}"));
            Assert.That(request.Headers.GetValues("X-Notification-Key").Single(),
                Is.EqualTo("test-only-notification-key-with-32-characters"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { email = "student@example.com" })
            };
        }));
        Assert.That(await new EmailRecipientClient(http, Configuration()).GetAsync(notification, default),
            Is.EqualTo("student@example.com"));
    }

    [Test]
    public void RemoteSmtpRequiresEncryption()
    {
        var options = new EmailOptions { Enabled = true, Host = "smtp.example.com", FromAddress = "test@example.com" };
        Assert.That(options.IsValid(), Is.True);
        options.EnableSsl = false;
        Assert.That(options.IsValid(), Is.False);
        options.Host = "localhost";
        Assert.That(options.IsValid(), Is.True);
        options.UserName = "test";
        Assert.That(options.IsValid(), Is.False);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["InternalServices:NotificationKey"] = "test-only-notification-key-with-32-characters",
            ["Services:StudentServiceUrl"] = "https://students.test/",
            ["Services:IdentityServiceUrl"] = "https://identity.test/"
        }).Build();

    private static Notification CreateNotification(string kind) => new(
        Guid.NewGuid(), "ChargeCreated", kind, Guid.NewGuid(), "New charge", "A charge was recorded.",
        "Charge", Guid.NewGuid(), DateTime.UtcNow);

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(response(request));
    }
}
