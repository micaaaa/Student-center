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
