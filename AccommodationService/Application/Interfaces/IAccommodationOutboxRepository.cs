using StudentCenter.AccommodationService.Domain.Entities;

namespace StudentCenter.AccommodationService.Application.Interfaces;

public interface IAccommodationOutboxRepository
{
    Task<IReadOnlyCollection<AccommodationOutboxMessage>> GetPendingAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
