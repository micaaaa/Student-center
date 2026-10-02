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
    public async Task StaffDoesNotDependOnStudentService()
    {
        var client = Create("STAFF", new Handler(_ => throw new IOException("Must not call")));
        var owner = await client.GetAsync(default);
        Assert.That(owner.UserId, Is.EqualTo(UserId));
        Assert.That(owner.StudentId, Is.Null);
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

    [Test]
    public async Task MissingProfileReturnsOnlyAccountScope()
    {
        var client = Create("STUDENT", new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.That((await client.GetAsync(default)).StudentId, Is.Null);
    }

    [TestCase(HttpStatusCode.Unauthorized, 401)]
    [TestCase(HttpStatusCode.Forbidden, 403)]
    [TestCase(HttpStatusCode.InternalServerError, 503)]
    public void LookupFailureIsNotAnEmptyInbox(HttpStatusCode status, int expected)
    {
        var client = Create("STUDENT", new Handler(_ => new HttpResponseMessage(status)));
        Assert.That(Assert.ThrowsAsync<OwnerLookupException>(() => client.GetAsync(default))!.StatusCode, Is.EqualTo(expected));
    }

    [Test]
    public void NetworkFailureIsUnavailable()
    {
        var client = Create("STUDENT", new Handler(_ => throw new HttpRequestException("Offline")));
        Assert.That(Assert.ThrowsAsync<OwnerLookupException>(() => client.GetAsync(default))!.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public void InvalidIdentityIsUnauthorized()
    {
        var client = Create("STAFF", new Handler(_ => throw new IOException()), "invalid");
        Assert.That(Assert.ThrowsAsync<OwnerLookupException>(() => client.GetAsync(default))!.StatusCode, Is.EqualTo(401));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(response(request));
        }
    }
}
