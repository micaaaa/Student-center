namespace StudentCenter.MaintenanceService.Domain.Entities;

public sealed class MaintenanceWorker
{
    private MaintenanceWorker()
    {
    }

    public MaintenanceWorker(Guid userId, string name, string specialization)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An existing staff user identifier is required.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Update(name, specialization, true);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Specialization { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, string specialization, bool isActive)
    {
        var cleanName = MaintenanceCategory.RequiredText(name, 200, "Worker name");
        var cleanSpecialization = MaintenanceCategory.RequiredText(specialization, 200, "Specialization");
        Name = cleanName;
        Specialization = cleanSpecialization;
        IsActive = isActive;
    }
}
