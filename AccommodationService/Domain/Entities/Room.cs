using StudentCenter.AccommodationService.Domain.Enums;
using StudentCenter.AccommodationService.Domain.Exceptions;

namespace StudentCenter.AccommodationService.Domain.Entities;

public sealed class Room
{
    private Room()
    {
    }

    public Room(Guid dormId, string roomNumber, int floor, int capacity, RoomStatus status = RoomStatus.Available)
    {
        if (dormId == Guid.Empty)
            throw new ArgumentException("A dorm is required.");
        Id = Guid.NewGuid();
        DormId = dormId;
        Update(roomNumber, floor, capacity, status);
    }

    public Guid Id { get; private set; }
    public Guid DormId { get; private set; }
    public string RoomNumber { get; private set; } = null!;
    public int Floor { get; private set; }
    public int Capacity { get; private set; }
    public int OccupiedBeds { get; private set; }
    public RoomStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string roomNumber, int floor, int capacity, RoomStatus status)
    {
        if (string.IsNullOrWhiteSpace(roomNumber) || roomNumber.Trim().Length > 20)
            throw new ArgumentException("Room number is required and must not exceed 20 characters.");
        if (capacity < 1)
            throw new ArgumentException("Room capacity must be positive.");
        if (!Enum.IsDefined(status) || status == RoomStatus.Full)
            throw new ArgumentException("Select AVAILABLE, MAINTENANCE or INACTIVE. FULL is calculated from occupancy.");
        if (capacity < OccupiedBeds)
            throw new AccommodationConflictException("Capacity cannot be lower than current occupancy.");
        if (OccupiedBeds > 0 && status is RoomStatus.Maintenance or RoomStatus.Inactive)
            throw new AccommodationConflictException("An occupied room cannot be made inactive or put under maintenance.");

        RoomNumber = roomNumber.Trim().ToUpperInvariant();
        Floor = floor;
        Capacity = capacity;
        Status = status == RoomStatus.Available && OccupiedBeds == capacity ? RoomStatus.Full : status;
    }

    public void ReserveBed()
    {
        if (Status != RoomStatus.Available || OccupiedBeds >= Capacity)
            throw new AccommodationConflictException("The room has no available bed.");
        OccupiedBeds++;
        if (OccupiedBeds == Capacity)
            Status = RoomStatus.Full;
    }

    public void ReleaseBed()
    {
        if (OccupiedBeds == 0)
            throw new AccommodationConflictException("The room has no occupied beds.");
        OccupiedBeds--;
        if (Status == RoomStatus.Full)
            Status = RoomStatus.Available;
    }
}
