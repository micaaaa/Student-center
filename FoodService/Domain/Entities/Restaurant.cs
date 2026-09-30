using StudentCenter.FoodService.Domain.Enums;

namespace StudentCenter.FoodService.Domain.Entities;

public sealed class Restaurant
{
    private Restaurant()
    {
    }

    public Restaurant(string name, string address)
    {
        Id = Guid.NewGuid();
        Update(name, address, RestaurantStatus.Active);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public RestaurantStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, string address, RestaurantStatus status)
    {
        var cleanName = RequiredText(name, 200, nameof(Name));
        var cleanAddress = RequiredText(address, 250, nameof(Address));
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("Unknown restaurant status.");
        }

        Name = cleanName;
        Address = cleanAddress;
        Status = status;
    }

    internal static string RequiredText(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{field} is required and must not exceed {maxLength} characters.");
        }

        return value.Trim();
    }
}
