using StudentCenter.AccommodationService.Domain.Enums;

namespace StudentCenter.AccommodationService.Domain.Entities;

public sealed class Dorm
{
    private Dorm()
    {
    }

    public Dorm(string name, string address, string city, string category, int capacity)
    {
        Id = Guid.NewGuid();
        Update(name, address, city, category, capacity, DormStatus.Active);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public int Capacity { get; private set; }
    public DormStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, string address, string city, string category, int capacity, DormStatus status)
    {
        var cleanName = RequiredText(name, 200, "Name");
        var cleanAddress = RequiredText(address, 250, "Address");
        var cleanCity = RequiredText(city, 100, "City");
        var cleanCategory = RequiredText(category, 100, "Category");
        if (capacity < 1)
            throw new ArgumentException("Dorm capacity must be positive.");
        if (!Enum.IsDefined(status))
            throw new ArgumentException("Unknown dorm status.");

        Name = cleanName;
        Address = cleanAddress;
        City = cleanCity;
        Category = cleanCategory;
        Capacity = capacity;
        Status = status;
    }

    private static string RequiredText(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new ArgumentException($"{field} is required and must not exceed {maxLength} characters.");
        return value.Trim();
    }
}
