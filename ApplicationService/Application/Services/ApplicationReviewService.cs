using StudentCenter.ApplicationService.Application.DTOs;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Application.Services;

public sealed class ApplicationReviewService(
    IApplicationRepository applications,
    IApplicationReviewRepository reviews,
    IDocumentRepository documents,
    IDocumentStorage storage)
{
    public async Task<IReadOnlyCollection<ApplicationResponse>> ListAsync(
        Guid? competitionId, ApplicationStatus? status, int page, CancellationToken ct, Guid? studentId = null)
    {
        if (page < 1 || page > int.MaxValue / 50)
            throw new ArgumentException("Page must be a positive number within the supported range.");
        if (status.HasValue && (!Enum.IsDefined(status.Value) || status == ApplicationStatus.Draft))
            throw new ArgumentException("Select a submitted application status.");

        return (await reviews.ListAsync(competitionId, status, page, ct, studentId)).Select(Map).ToArray();
    }

    public async Task<ApplicationResponse> GetAsync(Guid applicationId, CancellationToken ct) =>
        Map(await FindSubmittedAsync(applicationId, ct));

    public async Task<ApplicationResponse> StartReviewAsync(Guid applicationId, CancellationToken ct)
    {
        var application = await FindSubmittedAsync(applicationId, ct);
        application.StartReview();
        await reviews.SaveAsync(ct);
        return Map(application);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> ListDocumentsAsync(
        Guid applicationId, CancellationToken ct)
    {
        await FindSubmittedAsync(applicationId, ct);
        return (await documents.ListAsync(applicationId, ct)).Select(DocumentService.Map).ToArray();
    }

    public async Task<DocumentResponse> ReviewDocumentAsync(
        Guid applicationId, Guid documentId, ReviewDocumentRequest request, Guid reviewerId, CancellationToken ct)
    {
        var application = await FindSubmittedAsync(applicationId, ct);
        if (application.Status != ApplicationStatus.UnderReview)
            throw new ApplicationConflictException("Start application review before reviewing documents.");

        var document = await FindDocumentAsync(applicationId, documentId, ct);
        document.Review(request.Status, request.Comment, reviewerId);
        await reviews.SaveAsync(ct);
        return DocumentService.Map(document);
    }

    public async Task<DocumentDownload> DownloadAsync(
        Guid applicationId, Guid documentId, CancellationToken ct)
    {
        await FindSubmittedAsync(applicationId, ct);
        var document = await FindDocumentAsync(applicationId, documentId, ct);

        try
        {
            return new DocumentDownload(
                await storage.OpenAsync(document.FileReference, ct), document.ContentType, document.FileName);
        }
        catch (FileNotFoundException)
        {
            throw new KeyNotFoundException("Document file was not found.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new KeyNotFoundException("Document file was not found.");
        }
    }

    private async Task<StudentApplication> FindSubmittedAsync(Guid applicationId, CancellationToken ct)
    {
        var application = await applications.GetAsync(applicationId, ct);
        if (application is null || application.SubmittedAtUtc is null)
            throw new KeyNotFoundException("Submitted application was not found.");

        return application;
    }

    private async Task<ApplicationDocument> FindDocumentAsync(
        Guid applicationId, Guid documentId, CancellationToken ct) =>
        await documents.FindAsync(applicationId, documentId, ct)
        ?? throw new KeyNotFoundException("Document was not found.");

    private static ApplicationResponse Map(StudentApplication application) => new(
        application.Id,
        application.CompetitionId,
        application.StudentId,
        application.Status == ApplicationStatus.UnderReview ? "UNDER_REVIEW" : application.Status.ToString().ToUpperInvariant(),
        application.CreatedAtUtc,
        application.SubmittedAtUtc,
        application.Note);
}
