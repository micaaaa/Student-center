using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using StudentCenter.NotificationService.Infrastructure.ExternalServices;

namespace StudentCenter.NotificationService.Tests;

[TestFixture]
public sealed class CurrentOwnerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static CurrentNotificationOwner Create(string role, HttpMessageHandler handler, string? id = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id ?? UserId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "test"))
        };
        context.Request.Headers.Authorization = "Bearer forwarded-token";
        return new CurrentNotificationOwner(new HttpClient(handler) { BaseAddress = new Uri("http://student/") },
            new HttpContextAccessor { HttpContext = context });
    }

    [Test]
    public async Task StudentUsesAuthenticatedMeAndChecksReturnedAccount()
    {
        var profileId = Guid.NewGuid();
        var client = Create("STUDENT", new Handler(request =>
        {
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/students/me"));
            Assert.That(request.Headers.Authorization!.Parameter, Is.EqualTo("forwarded-token"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = profileId, userId = UserId })
            };
        }));
        Assert.That((await client.GetAsync(default)).StudentId, Is.EqualTo(profileId));
    }

    [Test]
    public void AnotherAccountProfileIsRejected()
    {
        var client = Create("STUDENT", new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = Guid.NewGuid(), userId = Guid.NewGuid() })
        }));
        var exception = Assert.ThrowsAsync<OwnerLookupException>(() => client.GetAsync(default));
        Assert.That(exception!.StatusCode, Is.EqualTo(503));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(response(request));
        }
    }
}
