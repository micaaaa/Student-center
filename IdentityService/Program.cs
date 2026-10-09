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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
{
    throw new InvalidOperationException("JWT key must contain at least 32 characters.");
}

builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<InitialAdminSettings>(builder.Configuration.GetSection(InitialAdminSettings.SectionName));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient<IStudentRegistrationClient,
    StudentCenter.IdentityService.Infrastructure.ExternalServices.StudentRegistrationClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:StudentServiceUrl"] ?? "https://localhost:49686/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddHttpClient("AccountLifecycle", client => client.Timeout = TimeSpan.FromSeconds(8));
builder.Services.AddScoped<IdentityDatabaseInitializer>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Enum.GetNames<Permission>())
    {
        options.AddPolicy(permission, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole("STAFF", "ADMIN")
            .RequireClaim("permission", permission));
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<IdentityDatabaseInitializer>();
    await databaseInitializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "IdentityService" }));

app.Run();

public partial class Program;
