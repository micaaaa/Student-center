using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using StudentCenter.FoodService.Application.Services;
using StudentCenter.FoodService.Domain.Exceptions;
using StudentCenter.FoodService.Infrastructure.ExternalServices;

namespace StudentCenter.FoodService.Tests;

public sealed class FoodStudentClientTests
{
    [Test]
    public async Task CurrentStudentLookupForwardsBearerAndReturnsProfileId()
    {
        var profileId = Guid.NewGuid();
        using var handler = new StubHandler(HttpStatusCode.OK, $"{{\"id\":\"{profileId}\"}}");
        using var http = Http(handler);
        var client = new FoodStudentClient(http, Context());

        Assert.That(await client.GetCurrentStudentIdAsync(default), Is.EqualTo(profileId));
        Assert.That(handler.LastPath, Is.EqualTo("/api/students/me"));
        Assert.That(handler.LastAuthorization, Is.EqualTo("Bearer test-token"));
    }

    [Test]
    public async Task StaffLookupUsesSummaryAndAcceptsActiveStudent()
    {
        var id = Guid.NewGuid();
        using var handler = new StubHandler(HttpStatusCode.OK, $"{{\"id\":\"{id}\",\"status\":\"ACTIVE\"}}");
        using var http = Http(handler);
        var client = new FoodStudentClient(http, Context());

        await client.EnsureActiveStudentAsync(id, default);

        Assert.That(handler.LastPath, Is.EqualTo($"/api/students/{id}/summary"));
    }

    [Test]
    public void InactiveStudentCannotReceiveEntitlement()
    {
        var id = Guid.NewGuid();
        using var handler = new StubHandler(HttpStatusCode.OK, $"{{\"id\":\"{id}\",\"status\":\"SUSPENDED\"}}");
        using var http = Http(handler);
        var client = new FoodStudentClient(http, Context());

        Assert.ThrowsAsync<FoodConflictException>(async () =>
            await client.EnsureActiveStudentAsync(id, default));
    }

    [TestCase(HttpStatusCode.NotFound, 404)]
    [TestCase(HttpStatusCode.Unauthorized, 401)]
    [TestCase(HttpStatusCode.Forbidden, 403)]
    [TestCase(HttpStatusCode.InternalServerError, 503)]
    public void UpstreamErrorsAreMapped(HttpStatusCode responseStatus, int expected)
    {
        using var handler = new StubHandler(responseStatus, "{}");
        using var http = Http(handler);
        var client = new FoodStudentClient(http, Context());

        var exception = Assert.ThrowsAsync<StudentLookupException>(async () =>
            await client.GetCurrentStudentIdAsync(default));

        Assert.That(exception!.StatusCode, Is.EqualTo(expected));
    }

    [TestCase("not json")]
    [TestCase("{}")]
    [TestCase("null")]
    public void InvalidProfileReturnsServiceUnavailable(string body)
    {
        using var handler = new StubHandler(HttpStatusCode.OK, body);
        using var http = Http(handler);
        var client = new FoodStudentClient(http, Context());

        var exception = Assert.ThrowsAsync<StudentLookupException>(async () =>
            await client.GetCurrentStudentIdAsync(default));
        Assert.That(exception!.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public void SummaryMustMatchRequestedStudentAndContainStatus()
    {
        var id = Guid.NewGuid();
        using var handler = new StubHandler(HttpStatusCode.OK, $"{{\"id\":\"{id}\"}}");
        using var http = Http(handler);
        var client = new FoodStudentClient(http, Context());

        var exception = Assert.ThrowsAsync<StudentLookupException>(async () =>
            await client.EnsureActiveStudentAsync(id, default));
        Assert.That(exception!.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public void MissingTokenDoesNotCallStudentService()
    {
        using var handler = new StubHandler(HttpStatusCode.OK, "{}");
        using var http = Http(handler);
        var client = new FoodStudentClient(http, new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        var exception = Assert.ThrowsAsync<StudentLookupException>(async () =>
            await client.GetCurrentStudentIdAsync(default));
        Assert.That(exception!.StatusCode, Is.EqualTo(401));
        Assert.That(handler.LastPath, Is.Null);
    }

    private static HttpClient Http(HttpMessageHandler handler)
    {
        return new HttpClient(handler) { BaseAddress = new Uri("http://student.test/") };
    }

    private static HttpContextAccessor Context()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastPath { get; private set; }
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.AbsolutePath;
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
