using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentCenter.MaintenanceService.Application.Interfaces;
using StudentCenter.MaintenanceService.Application.Services;
using StudentCenter.MaintenanceService.Infrastructure.Persistence;
using StudentCenter.MaintenanceService.Infrastructure.Repositories;
using StudentCenter.MaintenanceService.Infrastructure.ExternalServices;
using StudentCenter.MaintenanceService.Infrastructure.Messaging;
using StudentCenter.Messaging;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("MaintenanceDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'MaintenanceDb' is not configured.");
}

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32
    || string.IsNullOrWhiteSpace(jwt["Issuer"]) || string.IsNullOrWhiteSpace(jwt["Audience"]))
{
    throw new InvalidOperationException("Configure Jwt Issuer, Audience and a signing key of at least 32 characters.");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<MaintenanceDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
builder.Services.AddScoped<MaintenanceRequestService>();
builder.Services.AddScoped<IWorkRepository, WorkRepository>();
builder.Services.AddScoped<MaintenanceWorkService>();
builder.Services.AddScoped<IMaintenanceOutboxRepository, MaintenanceOutboxRepository>();
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddHostedService<MaintenanceOutboxWorker>();
builder.Services.AddHttpClient<IStaffDirectoryClient, StaffDirectoryClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityServiceUrl"] ?? "https://localhost:49586/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ICurrentStudentContext, CurrentStudentContext>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
foreach (var (name, fallbackUrl) in new[]
{
    ("StudentService", "https://localhost:49686/"),
    ("AccommodationService", "https://localhost:49886/")
})
{
    builder.Services.AddHttpClient(name, client =>
    {
        client.BaseAddress = new Uri(builder.Configuration[$"Services:{name}Url"] ?? fallbackUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = StudentCenter.Security.AccountAccessValidation.Events();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageMaintenance", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("STAFF", "ADMIN")
        .RequireClaim("permission", "ManageMaintenance"));
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>();
    await db.Database.MigrateAsync();
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
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "MaintenanceService" }));
app.Run();


