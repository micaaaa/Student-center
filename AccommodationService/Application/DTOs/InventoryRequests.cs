using System.ComponentModel.DataAnnotations;
using StudentCenter.AccommodationService.Domain.Enums;

namespace StudentCenter.AccommodationService.Application.DTOs;

public sealed class DormRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(250)]
    public string Address { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string City { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string Category { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Capacity { get; init; }

    [EnumDataType(typeof(DormStatus))]
    public DormStatus Status { get; init; } = DormStatus.Active;
}

public sealed class RoomRequest
{
    [Required, MaxLength(20)]
    public string RoomNumber { get; init; } = string.Empty;

    public int Floor { get; init; }

    [Range(1, int.MaxValue)]
    public int Capacity { get; init; }

    [EnumDataType(typeof(RoomStatus))]
    public RoomStatus Status { get; init; } = RoomStatus.Available;
}

public sealed record DormResponse(
    Guid Id, string Name, string Address, string City, string Category, int Capacity, string Status);

public sealed record RoomResponse(
    Guid Id, Guid DormId, string RoomNumber, int Floor, int Capacity, int OccupiedBeds, string Status);
