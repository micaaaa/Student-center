namespace StudentCenter.StudentService.Application.DTOs;

public sealed record StudentSearchResponse(
    IReadOnlyList<StudentSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
