using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Domain.Exceptions;
using StudentCenter.MaintenanceService.Infrastructure.ExternalServices;

namespace StudentCenter.MaintenanceService.Tests;

public sealed class CurrentStudentContextTests
{
    [Test]
    public async Task ActiveAccommodationUsesAuthenticatedEndpointAndRoom()
    {
        var room = Guid.NewGuid();
        var accommodation = Guid.NewGuid();
        using var handler = new Handler(HttpStatusCode.OK,
            $"{{\"id\":\"{accommodation}\",\"status\":\"ACTIVE\",\"room\":{{\"id\":\"{room}\"}}}}");
        var client = Client(handler);

        var result = await client.GetActiveAccommodationAsync(default);

        Assert.That(result.Id, Is.EqualTo(accommodation));
        Assert.That(result.RoomId, Is.EqualTo(room));
        Assert.That(handler.Path, Is.EqualTo("/api/accommodations/me"));
        Assert.That(handler.Authorization, Is.EqualTo("Bearer test-token"));
    }

    [TestCase("ASSIGNED")]
    [TestCase("COMPLETED")]
    [TestCase("CANCELLED")]
    public void NonActiveAccommodationCannotBeUsed(string status)
    {
        using var handler = new Handler(HttpStatusCode.OK,
            $"{{\"id\":\"{Guid.NewGuid()}\",\"status\":\"{status}\",\"room\":{{\"id\":\"{Guid.NewGuid()}\"}}}}");
        Assert.ThrowsAsync<MaintenanceConflictException>(async () =>
            await Client(handler).GetActiveAccommodationAsync(default));
    }

    [TestCase("{}")]
    [TestCase("null")]
    [TestCase("invalid")]
    public void InvalidAccommodationResponseReturns503(string body)
    {
        using var handler = new Handler(HttpStatusCode.OK, body);
        var exception = Assert.ThrowsAsync<ServiceLookupException>(async () =>
            await Client(handler).GetActiveAccommodationAsync(default));
        Assert.That(exception!.StatusCode, Is.EqualTo(503));
    }

    [TestCase(HttpStatusCode.NotFound, 404)]
    [TestCase(HttpStatusCode.Unauthorized, 401)]
    [TestCase(HttpStatusCode.Forbidden, 403)]
    [TestCase(HttpStatusCode.InternalServerError, 503)]
    public void UpstreamErrorsAreMapped(HttpStatusCode code, int expected)
    {
        using var handler = new Handler(code, "{}");
        var exception = Assert.ThrowsAsync<ServiceLookupException>(async () =>
            await Client(handler).GetStudentIdAsync(default));
        Assert.That(exception!.StatusCode, Is.EqualTo(expected));
    }

    [Test]
    public async Task StudentLookupUsesProfileId()
    {
        var id = Guid.NewGuid();
        using var handler = new Handler(HttpStatusCode.OK, $"{{\"id\":\"{id}\"}}");
        Assert.That(await Client(handler).GetStudentIdAsync(default), Is.EqualTo(id));
        Assert.That(handler.Path, Is.EqualTo("/api/students/me"));
    }

    [Test]
    public void MissingBearerDoesNotSendRequest()
    {
        using var handler = new Handler(HttpStatusCode.OK, "{}");
        var client = new CurrentStudentContext(new Factory(handler),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        var exception = Assert.ThrowsAsync<ServiceLookupException>(async () =>
            await client.GetStudentIdAsync(default));
        Assert.That(exception!.StatusCode, Is.EqualTo(401));
        Assert.That(handler.Path, Is.Null);
    }

    private static CurrentStudentContext Client(Handler handler)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        return new CurrentStudentContext(new Factory(handler), new HttpContextAccessor { HttpContext = context });
    }

    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://service.test/") };
        }
    }

    private sealed class Handler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri?.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
