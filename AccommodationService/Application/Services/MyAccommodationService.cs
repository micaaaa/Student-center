using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Interfaces;

namespace StudentCenter.AccommodationService.Application.Services;

public sealed class MyAccommodationService(ICurrentStudentClient students, IStudentAccommodationReader reader)
{
    public async Task<MyAccommodationResponse> GetCurrentAsync(CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return await reader.GetCurrentAsync(studentId, ct)
            ?? throw new KeyNotFoundException("You do not have a current accommodation.");
    }

    public async Task<IReadOnlyCollection<MyAccommodationResponse>> GetHistoryAsync(CancellationToken ct)
    {
        var studentId = await students.GetCurrentStudentIdAsync(ct);
        return await reader.GetHistoryAsync(studentId, ct);
    }
}
