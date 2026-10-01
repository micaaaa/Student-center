using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudentCenter.MaintenanceService.Infrastructure.Persistence;

// Migration tooling creates only a context; it does not start the API or apply migrations.
public sealed class MaintenanceDbContextFactory : IDesignTimeDbContextFactory<MaintenanceDbContext>
{
    public MaintenanceDbContext CreateDbContext(string[] args)
    {
        var directory = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(directory, "MaintenanceService.csproj")))
            directory = Path.Combine(directory, "MaintenanceService");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("MaintenanceDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Server=.\\SQLEXPRESS;Database=StudentCenter.MaintenanceDb;Trusted_Connection=True;Encrypt=False";

        return new MaintenanceDbContext(new DbContextOptionsBuilder<MaintenanceDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}


