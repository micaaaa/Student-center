using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudentCenter.FoodService.Infrastructure.Persistence;

// Migration tooling creates only a context; it does not start the API or apply migrations.
public sealed class FoodDbContextFactory : IDesignTimeDbContextFactory<FoodDbContext>
{
    public FoodDbContext CreateDbContext(string[] args)
    {
        var directory = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(directory, "FoodService.csproj")))
            directory = Path.Combine(directory, "FoodService");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("FoodDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Server=.\\SQLEXPRESS;Database=StudentCenter.FoodDb;Trusted_Connection=True;Encrypt=False";

        return new FoodDbContext(new DbContextOptionsBuilder<FoodDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}

