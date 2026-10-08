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

    [TestCase("ASSIGNED")]
    public void NonActiveAccommodationCannotBeUsed(string status)
    {
        using var handler = new Handler(HttpStatusCode.OK,
            $"{{\"id\":\"{Guid.NewGuid()}\",\"status\":\"{status}\",\"room\":{{\"id\":\"{Guid.NewGuid()}\"}}}}");
        Assert.ThrowsAsync<MaintenanceConflictException>(async () =>
            await Client(handler).GetActiveAccommodationAsync(default));
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
