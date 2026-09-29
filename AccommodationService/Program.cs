using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentCenter.AccommodationService.Application.Interfaces;
using StudentCenter.AccommodationService.Application.Services;
using StudentCenter.AccommodationService.Infrastructure.Persistence;
using StudentCenter.AccommodationService.Infrastructure.Repositories;
using StudentCenter.AccommodationService.Infrastructure.Messaging;
using StudentCenter.Messaging;
using StudentCenter.AccommodationService.Infrastructure.ExternalServices;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("AccommodationDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'AccommodationDb' is not configured.");

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32
    || string.IsNullOrWhiteSpace(jwt["Issuer"]) || string.IsNullOrWhiteSpace(jwt["Audience"]))
    throw new InvalidOperationException("Configure Jwt Issuer, Audience and a signing key of at least 32 characters.");

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

builder.Services.AddDbContext<AccommodationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<IAssignmentRepository, AssignmentRepository>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddScoped<IStudentAccommodationReader, StudentAccommodationReader>();
builder.Services.AddScoped<MyAccommodationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ICurrentStudentClient, CurrentStudentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:StudentServiceUrl"]
        ?? "https://localhost:49686/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddHostedService<EligibilityConsumer>();
builder.Services.AddScoped<IAccommodationOutboxRepository, AccommodationOutboxRepository>();
builder.Services.AddHostedService<AccommodationOutboxWorker>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
    options.AddPolicy("ManageAccommodation", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "ManageAccommodation"));
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AccommodationDbContext>();
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
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "AccommodationService" }));
app.Run();
