using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using StudentCenter.ApplicationService.API.Controllers;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;
using Service = StudentCenter.ApplicationService.Application.Services.ApplicationService;

namespace StudentCenter.ApplicationService.Tests;

public sealed class ApplicationLifecycleTests
{
    private Store store = null!;

    private StudentClient student = null!;

    private Service service = null!;

    private Competition competition = null!;

    [SetUp]
    public void SetUp()
    {
        store = new Store();
        student = new StudentClient();
        competition = new Competition("2026/27", "Accommodation", null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        competition.Open();
        store.Competition = competition;
        service = new Service(store, store, student);
    }

    [Test]
    public async Task StudentCanCreateReadEditSubmitAndWithdraw()
    {
        var created = await service.CreateAsync(new(competition.Id, " original "), default);
        Assert.That(created.Status, Is.EqualTo("DRAFT"));
        Assert.That((await service.GetAsync(created.Id, default)).Note, Is.EqualTo("original"));
        Assert.That((await service.GetMineAsync(default)).Single().Id, Is.EqualTo(created.Id));
        Assert.That((await service.UpdateAsync(created.Id, new(" edited "), default)).Note, Is.EqualTo("edited"));
        var submitted = await service.SubmitAsync(created.Id, default);
        Assert.That(submitted.Status, Is.EqualTo("SUBMITTED"));
        Assert.That(submitted.SubmittedAtUtc, Is.Not.Null);
        var withdrawn = await service.WithdrawAsync(created.Id, default);
        Assert.That(withdrawn.Status, Is.EqualTo("WITHDRAWN"));
        Assert.That(withdrawn.SubmittedAtUtc, Is.EqualTo(submitted.SubmittedAtUtc));
    }

    [TestCase("read")]
    [TestCase("edit")]
    [TestCase("submit")]
    [TestCase("withdraw")]
    public void ForeignApplicationIsHiddenAndNotModified(string operation)
    {
        var application = new StudentApplication(competition.Id, Guid.NewGuid(), "original");
        store.Items.Add(application);
        Assert.ThrowsAsync<KeyNotFoundException>(() => Operate(operation, application.Id));
        Assert.That(application.Status, Is.EqualTo(ApplicationStatus.Draft));
        Assert.That(application.Note, Is.EqualTo("original"));
        Assert.That(store.Saves, Is.Zero);
    }

    [TestCase("read")]
    [TestCase("edit")]
    [TestCase("submit")]
    [TestCase("withdraw")]
    public void MissingApplicationIsNotFound(string operation) =>
        Assert.ThrowsAsync<KeyNotFoundException>(() => Operate(operation, Guid.NewGuid()));

    [TestCase("future", false)]
    [TestCase("expired", false)]
    [TestCase("closed", false)]
    [TestCase("future", true)]
    [TestCase("expired", true)]
    [TestCase("closed", true)]
    public void CreationAndSubmissionRequireActiveCompetition(string state, bool submit)
    {
        var now = DateTime.UtcNow;
        competition = new Competition(
            "2026/27",
            "Test",
            null,
            state == "future" ? now.AddDays(1) : now.AddDays(-2),
            state == "expired" ? now.AddDays(-1) : now.AddDays(2));
        competition.Open();
        if (state == "closed")
            competition.Close();
        store.Competition = competition;
        var application = new StudentApplication(competition.Id, student.Id);
        if (submit)
            store.Items.Add(application);
        Assert.ThrowsAsync<ApplicationConflictException>(
            async () =>
        {
            if (submit)
                await service.SubmitAsync(application.Id, default);
            else
                await service.CreateAsync(new(competition.Id), default);
        });
        Assert.That(application.Status, Is.EqualTo(ApplicationStatus.Draft));
        Assert.That(store.Saves, Is.Zero);
        Assert.That(store.Items.Count, Is.EqualTo(submit ? 1 : 0));
    }

    [Test]
    public async Task DuplicateIncludingWithdrawnApplicationIsRejected()
    {
        var created = await service.CreateAsync(new(competition.Id), default);
        await service.WithdrawAsync(created.Id, default);
        Assert.ThrowsAsync<ApplicationConflictException>(() => service.CreateAsync(new(competition.Id), default));
        Assert.That(store.Items.Count, Is.EqualTo(1));
    }

    [TestCase(ApplicationStatus.Submitted)]
    [TestCase(ApplicationStatus.UnderReview)]
    [TestCase(ApplicationStatus.Accepted)]
    [TestCase(ApplicationStatus.Rejected)]
    [TestCase(ApplicationStatus.Withdrawn)]
    public void NonDraftCannotBeEditedOrSubmitted(ApplicationStatus status)
    {
        var application = WithStatus(status);
        Assert.Throws<ApplicationConflictException>(() => application.UpdateNote("changed"));
        Assert.Throws<ApplicationConflictException>(() => application.Submit());
        Assert.That(application.Note, Is.EqualTo("original"));
        Assert.That(application.Status, Is.EqualTo(status));
    }

    [TestCase(ApplicationStatus.UnderReview)]
    [TestCase(ApplicationStatus.Accepted)]
    [TestCase(ApplicationStatus.Rejected)]
    [TestCase(ApplicationStatus.Withdrawn)]
    public void ProcessingAndTerminalStatesCannotBeWithdrawn(ApplicationStatus status)
    {
        var application = WithStatus(status);
        Assert.Throws<ApplicationConflictException>(() => application.Withdraw());
        Assert.That(application.Status, Is.EqualTo(status));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task DraftNoteCanBeCleared(string? note)
    {
        var created = await service.CreateAsync(new(competition.Id, "original"), default);
        Assert.That((await service.UpdateAsync(created.Id, new(note), default)).Note, Is.Null);
    }

    [Test]
    public async Task ControllerMapsMissingAndConflict()
    {
        var controller = new ApplicationsController(service);
        Assert.That((await controller.Get(Guid.NewGuid(), default)).Result, Is.TypeOf<NotFoundObjectResult>());
        var created = await service.CreateAsync(new(competition.Id), default);
        await service.SubmitAsync(created.Id, default);
        Assert.That((await controller.Submit(created.Id, default)).Result, Is.TypeOf<ConflictObjectResult>());
    }

    [Test]
    public async Task ControllerMapsStudentServiceFailureTo503()
    {
        student.Fail = true;
        var result = await new ApplicationsController(service).GetMine(default);
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(503));
    }

    private async Task Operate(string operation, Guid id)
    {
        switch (operation)
        {
            case "read":
                await service.GetAsync(id, default);
                break;
            case "edit":
                await service.UpdateAsync(id, new("changed"), default);
                break;
            case "submit":
                await service.SubmitAsync(id, default);
                break;
            case "withdraw":
                await service.WithdrawAsync(id, default);
                break;
        }
    }

    private StudentApplication WithStatus(ApplicationStatus status)
    {
        var application = new StudentApplication(competition.Id, student.Id, "original");
        // Materialize states that will later be set by staff workflows.
        typeof(StudentApplication).GetProperty(nameof(StudentApplication.Status))!.SetValue(application, status);
        return application;
    }

    private sealed class StudentClient : IStudentClient
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Fail { get; set; }

        public Task<Guid> GetCurrentStudentIdAsync(CancellationToken ct = default) =>
            Fail ? throw new HttpRequestException("Unavailable") : Task.FromResult(Id);
    }

    private sealed class Store : IApplicationRepository, ICompetitionRepository
    {
        public List<StudentApplication> Items { get; } = [];
        public Competition? Competition { get; set; }
        public int Saves { get; private set; }

        public Task<StudentApplication?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.SingleOrDefault(x => x.Id == id));

        public Task<StudentApplication?> GetForStudentAsync(Guid competitionId, Guid studentId, CancellationToken ct = default) =>
            Task.FromResult(Items.SingleOrDefault(x => x.CompetitionId == competitionId && x.StudentId == studentId));

        public Task<IReadOnlyCollection<StudentApplication>> GetMineAsync(Guid studentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<StudentApplication>>(Items.Where(x => x.StudentId == studentId).ToArray());

        public Task AddAsync(StudentApplication application, CancellationToken ct = default)
        {
            Items.Add(application);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public Task<Competition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Competition?.Id == id ? Competition : null);

        public Task<IReadOnlyCollection<Competition>> GetAllAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Competition competition, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
