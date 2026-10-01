namespace StudentCenter.MaintenanceService.Domain.Entities;

public sealed class MaintenanceCategory
{
    private MaintenanceCategory()
    {
    }

    public MaintenanceCategory(string name, string? description)
    {
        Id = Guid.NewGuid();
        Update(name, description, true);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, string? description, bool isActive)
    {
        var cleanName = RequiredText(name, 100, "Category name");
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleanDescription?.Length > 1000)
        {
            throw new ArgumentException("Category description must not exceed 1000 characters.");
        }

        Name = cleanName;
        Description = cleanDescription;
        IsActive = isActive;
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
