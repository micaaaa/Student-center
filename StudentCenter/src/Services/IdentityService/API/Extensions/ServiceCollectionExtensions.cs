using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Application.Services;
using StudentCenter.IdentityService.Domain.Enums;
using StudentCenter.IdentityService.Infrastructure.Persistence;
using StudentCenter.IdentityService.Infrastructure.Repositories;
using StudentCenter.IdentityService.Infrastructure.Security;

namespace StudentCenter.IdentityService.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");

        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
        services.Configure<InitialAdminSettings>(configuration.GetSection(InitialAdminSettings.SectionName));
        services.AddScoped<IdentityDatabaseInitializer>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        if (string.IsNullOrWhiteSpace(settings.Key) || settings.Key.Length < 32)
        {
            throw new InvalidOperationException("JWT key must contain at least 32 characters.");
        }

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var permission in Enum.GetNames<Permission>())
            {
                options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            }
        });
        return services;
    }
}
