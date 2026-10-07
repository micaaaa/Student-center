using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentCenter.NotificationService.Application.Interfaces;
using StudentCenter.NotificationService.Infrastructure.Persistence;
using StudentCenter.NotificationService.Infrastructure.Repositories;
using StudentCenter.NotificationService.Infrastructure.ExternalServices;
using StudentCenter.NotificationService.Infrastructure.Messaging;
using StudentCenter.Messaging;
using StudentCenter.NotificationService.Infrastructure.Email;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("NotificationDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'NotificationDb' is not configured.");
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

builder.Services.AddDbContext<NotificationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<StudentCenter.NotificationService.Application.Services.NotificationService>();
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddHostedService<NotificationConsumer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<EmailOptions>().Bind(builder.Configuration.GetSection("Email"))
    .Validate(options => options.IsValid(), "Email configuration is invalid.")
    .Validate(options => !options.Enabled
        || builder.Configuration["InternalServices:NotificationKey"] is { Length: >= 32 and <= 256 },
        "Configure the shared internal notification key before enabling email.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IEmailRecipientClient, EmailRecipientClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ICurrentNotificationOwner, CurrentNotificationOwner>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:StudentServiceUrl"] ?? "https://localhost:49686/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

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

builder.Services.AddAuthorization();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
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
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "NotificationService" }));
app.Run();



