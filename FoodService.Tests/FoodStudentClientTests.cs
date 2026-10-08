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
    public void InactiveStudentCannotReceiveEntitlement()
    {
        var id = Guid.NewGuid();
        using var handler = new StubHandler(HttpStatusCode.OK, $"{{\"id\":\"{id}\",\"status\":\"SUSPENDED\"}}");
        using var http = Http(handler);
        var client = new FoodStudentClient(http, Context());

        Assert.ThrowsAsync<FoodConflictException>(async () =>
            await client.EnsureActiveStudentAsync(id, default));
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
