using Microsoft.Extensions.DependencyInjection;

namespace StudentCenter.ApplicationService.Infrastructure.Security;

public static class ApplicationAuthorization
{
    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ManageApplications", policy => policy
                .RequireAuthenticatedUser()
                .RequireRole("STAFF", "ADMIN")
                .RequireClaim("permission", "ManageApplications"));
        });
        return services;
    }
}
