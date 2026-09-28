using System.Security.Cryptography;
using System.Text;
using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class ApplicationScoringService(
    IApplicationRepository applications,
    IDocumentRepository documents,
    IScoringRepository scores,
    IStudentClient students)
{
    public async Task<ScoringResponse> CalculateAsync(
        Guid applicationId, CalculateScoreRequest request, Guid userId, CancellationToken ct)
    {
        var application = await FindApplicationAsync(applicationId, ct);
        if (application.Status != ApplicationStatus.UnderReview)
            throw new ApplicationConflictException("Only an application under review can be scored.");

        var reviewedDocuments = await documents.ListAsync(applicationId, ct);
        if (!AllDocumentsValid(reviewedDocuments))
            throw new ApplicationConflictException("At least one document is required and every document must be VALID.");

        if (request.AcademicPoints is null || request.IncomePoints is null || request.ECTSPoints is null
            || request.StudyYearPoints is null || request.AdditionalPoints is null)
            throw new ArgumentException("All five scoring categories are required.");

        var existing = await scores.FindAsync(applicationId, ct);
        var score = existing ?? new ScoringResult(applicationId);
        score.Calculate(
            request.AcademicPoints.Value,
            request.IncomePoints.Value,
            request.ECTSPoints.Value,
            request.StudyYearPoints.Value,
            request.AdditionalPoints.Value,
            userId,
            Fingerprint(reviewedDocuments));

        if (existing is null)
            await scores.AddAsync(score, ct);
        await scores.SaveAsync(ct);

        return Map(score, true);
    }

    public async Task<ScoringResponse> GetForStaffAsync(Guid applicationId, CancellationToken ct)
    {
        var application = await FindApplicationAsync(applicationId, ct);
        if (application.SubmittedAtUtc is null)
            throw new KeyNotFoundException("Submitted application was not found.");

        return await GetScoreAsync(applicationId, ct);
    }

    public async Task<ScoringResponse> GetMineAsync(Guid applicationId, CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        var application = await FindApplicationAsync(applicationId, ct);
        if (application.StudentId != studentId)
            throw new KeyNotFoundException("Application was not found.");

        return await GetScoreAsync(applicationId, ct);
    }

    private async Task<ScoringResponse> GetScoreAsync(Guid applicationId, CancellationToken ct)
    {
        var score = await scores.FindAsync(applicationId, ct)
            ?? throw new KeyNotFoundException("The application has not been scored yet.");
        var reviewedDocuments = await documents.ListAsync(applicationId, ct);
        var isCurrent = AllDocumentsValid(reviewedDocuments)
            && score.DocumentReviewFingerprint == Fingerprint(reviewedDocuments);
        return Map(score, isCurrent);
    }

    private async Task<StudentApplication> FindApplicationAsync(Guid applicationId, CancellationToken ct) =>
        await applications.GetAsync(applicationId, ct)
        ?? throw new KeyNotFoundException("Application was not found.");

    private static bool AllDocumentsValid(IReadOnlyCollection<ApplicationDocument> items) =>
        items.Count > 0 && items.All(document => document.Status == DocumentStatus.Valid);

    private static string Fingerprint(IEnumerable<ApplicationDocument> items)
    {
        var snapshot = string.Join("|", items.OrderBy(document => document.Id).Select(document =>
            $"{document.Id:N}:{(int)document.Status}:{document.ReviewedAtUtc?.Ticks}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }

    private static ScoringResponse Map(ScoringResult score, bool isCurrent) => new(
        score.Id,
        score.ApplicationId,
        score.AcademicPoints,
        score.IncomePoints,
        score.ECTSPoints,
        score.StudyYearPoints,
        score.AdditionalPoints,
        score.TotalPoints,
        score.CalculatedAtUtc,
        score.CalculatedByUserId,
        isCurrent);
}
