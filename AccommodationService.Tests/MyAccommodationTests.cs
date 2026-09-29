using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;
using StudentCenter.AccommodationService.API.Controllers;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Application.Services;
using StudentCenter.AccommodationService.Infrastructure.ExternalServices;
using StudentCenter.AccommodationService.Infrastructure.Persistence;
using StudentCenter.AccommodationService.Infrastructure.Repositories;

namespace StudentCenter.AccommodationService.Tests;

public sealed class MyAccommodationTests
{
    [Test]
    public void ReadQueriesTranslateToSqlBeforeOpeningTheDatabase()
    {
        using var db = new AccommodationDbContext(new DbContextOptionsBuilder<AccommodationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true")
            .AddInterceptors(new PreventDatabaseConnection())
            .Options);
        var reader = new StudentAccommodationReader(db);
        Assert.ThrowsAsync<DatabaseNotOpenedException>(() => reader.GetCurrentAsync(Guid.NewGuid(), default));
        Assert.ThrowsAsync<DatabaseNotOpenedException>(() => reader.GetHistoryAsync(Guid.NewGuid(), default));
    }

    [Test]
    public async Task CurrentAndHistoryUseTheResolvedStudentRatherThanIdentityAccountId()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var students = new StudentStub(firstId);
        var reader = new ReaderStub();
        reader.Rows[firstId] = [Response("COMPLETED"), Response("ASSIGNED")];
        reader.Rows[secondId] = [Response("ACTIVE")];
        var service = new MyAccommodationService(students, reader);

        Assert.That((await service.GetCurrentAsync(default)).Id, Is.EqualTo(reader.Rows[firstId][1].Id));
        Assert.That(await service.GetHistoryAsync(default), Is.EquivalentTo(reader.Rows[firstId]));
        students.Id = secondId;
        Assert.That((await service.GetCurrentAsync(default)).Id, Is.EqualTo(reader.Rows[secondId][0].Id));
        Assert.That(await service.GetHistoryAsync(default), Is.EquivalentTo(reader.Rows[secondId]));
    }

    [Test]
    public async Task NoCurrentAccommodationReturns404ButHistoryCanBeEmptyOrCompleted()
    {
        var students = new StudentStub(Guid.NewGuid());
        var reader = new ReaderStub();
        var service = new MyAccommodationService(students, reader);
        var controller = new MyAccommodationController(service);
        Assert.That(await controller.Current(default), Is.TypeOf<NotFoundObjectResult>());
        Assert.That(await service.GetHistoryAsync(default), Is.Empty);
        reader.Rows[students.Id] = [Response("COMPLETED"), Response("CANCELLED")];
        Assert.That(await controller.Current(default), Is.TypeOf<NotFoundObjectResult>());
        Assert.That(await service.GetHistoryAsync(default), Has.Count.EqualTo(2));
    }

    [TestCase("STUDENT", true)]
    [TestCase("STAFF", false)]
    [TestCase("ADMIN", false)]
    public async Task StudentEndpointsRequireStudentRole(string role, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        using var provider = services.BuildServiceProvider();
        var metadata = typeof(MyAccommodationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "test");
        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(new ClaimsPrincipal(identity), null, policy!);
        Assert.That(result.Succeeded, Is.EqualTo(allowed));
    }

    [Test]
    public async Task ClientForwardsEachRequestsTokenAndReadsProfileId()
    {
        var profileId = Guid.NewGuid();
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"id\":\"{profileId}\"}}")
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://student.test/") };
        var context = Context("Bearer first-token");
        var client = new CurrentStudentClient(http, context);
        Assert.That(await client.GetCurrentStudentIdAsync(default), Is.EqualTo(profileId));
        Assert.That(handler.Headers.Single(), Is.EqualTo("Bearer first-token"));
        context.HttpContext!.Request.Headers.Authorization = "Bearer second-token";
        await client.GetCurrentStudentIdAsync(default);
        Assert.That(handler.Headers.Last(), Is.EqualTo("Bearer second-token"));
        Assert.That(handler.Path, Is.EqualTo("/api/students/me"));
        Assert.That(http.DefaultRequestHeaders.Authorization, Is.Null);
    }

    [TestCase(404, 404)]
    [TestCase(401, 401)]
    [TestCase(403, 403)]
    [TestCase(500, 503)]
    [TestCase(302, 503)]
    public async Task UpstreamFailuresReturnExplicitStatusWithoutReadingAccommodation(int upstream, int expected)
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage((HttpStatusCode)upstream)))
        {
            BaseAddress = new Uri("https://student.test/")
        };
        var reader = new ReaderStub();
        var controller = new MyAccommodationController(new MyAccommodationService(
            new CurrentStudentClient(http, Context("Bearer token")), reader));
        var response = (ObjectResult)await controller.Current(default);
        Assert.That(response.StatusCode, Is.EqualTo(expected));
        Assert.That(reader.Reads, Is.Zero);
    }

    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("not-json")]
    public void InvalidUpstreamProfileIsUnavailableRatherThanAnEmptyHistory(string body)
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        }))
        { BaseAddress = new Uri("https://student.test/") };
        var client = new CurrentStudentClient(http, Context("Bearer token"));
        Assert.That(Assert.ThrowsAsync<StudentLookupException>(() =>
            client.GetCurrentStudentIdAsync(default))!.StatusCode, Is.EqualTo(503));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NetworkFailureOrTimeoutReturns503(bool timeout)
    {
        using var http = new HttpClient(new Handler(_ =>
        {
            if (timeout)
                throw new TaskCanceledException();
            throw new HttpRequestException();
        }))
        {
            BaseAddress = new Uri("https://student.test/")
        };
        var client = new CurrentStudentClient(http, Context("Bearer token"));
        Assert.That(Assert.ThrowsAsync<StudentLookupException>(() =>
            client.GetCurrentStudentIdAsync(default))!.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public void MissingBearerTokenDoesNotCallStudentService()
    {
        var handler = new Handler(_ => throw new AssertionException("No request should be sent."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://student.test/") };
        var client = new CurrentStudentClient(http, Context(""));
        Assert.That(Assert.ThrowsAsync<StudentLookupException>(() =>
            client.GetCurrentStudentIdAsync(default))!.StatusCode, Is.EqualTo(401));
        Assert.That(handler.Headers, Is.Empty);
    }

    private static HttpContextAccessor Context(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = token;
        return new HttpContextAccessor { HttpContext = context };
    }

    private static MyAccommodationResponse Response(string status) => new(
        Guid.NewGuid(), "2026/2027", status, DateTime.UtcNow, null, null, null,
        new StudentDormResponse(Guid.NewGuid(), "Dom A", "Adresa 1", "Novi Sad"),
        new StudentRoomResponse(Guid.NewGuid(), "101", 1));

    private sealed class StudentStub(Guid id) : ICurrentStudentClient
    {
        public Guid Id { get; set; } = id;
        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct) => Task.FromResult(Id);
    }

    private sealed class ReaderStub : IStudentAccommodationReader
    {
        public Dictionary<Guid, MyAccommodationResponse[]> Rows { get; } = [];
        public int Reads { get; private set; }

        public Task<MyAccommodationResponse?> GetCurrentAsync(Guid studentId, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(Rows.GetValueOrDefault(studentId, [])
                .SingleOrDefault(item => item.Status is "ASSIGNED" or "ACTIVE"));
        }

        public Task<IReadOnlyCollection<MyAccommodationResponse>> GetHistoryAsync(Guid studentId, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<IReadOnlyCollection<MyAccommodationResponse>>(Rows.GetValueOrDefault(studentId, []));
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string?> Headers { get; } = [];
        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Headers.Add(request.Headers.Authorization?.ToString());
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class DatabaseNotOpenedException : Exception;

    private sealed class PreventDatabaseConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            System.Data.Common.DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new DatabaseNotOpenedException();
    }
}
