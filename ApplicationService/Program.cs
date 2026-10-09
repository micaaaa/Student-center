using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentCenter.ApplicationService.Application.Interfaces;
using StudentCenter.ApplicationService.Application.Services;
using StudentCenter.ApplicationService.Infrastructure.ExternalServices;
using StudentCenter.ApplicationService.Infrastructure.Persistence;
using StudentCenter.ApplicationService.Infrastructure.Repositories;
using StudentCenter.ApplicationService.Infrastructure.Storage;
using StudentCenter.ApplicationService.Infrastructure.Messaging;
using StudentCenter.ApplicationService.Infrastructure.Security;
using StudentCenter.Messaging;

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = builder.Configuration.GetSection("Jwt");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("ApplicationDb")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IStudentClient, StudentClient>(client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:StudentServiceUrl"] ?? "https://localhost:49686/"));

builder.Services.AddScoped<ICompetitionRepository, CompetitionRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<IApplicationReviewRepository, ApplicationReviewRepository>();
builder.Services.AddScoped<ApplicationReviewService>();
builder.Services.AddScoped<IScoringRepository, ScoringRepository>();
builder.Services.AddScoped<ApplicationScoringService>();
builder.Services.AddScoped<IRankingRepository, RankingRepository>();
builder.Services.AddScoped<PreliminaryRankingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddHostedService<EligibilityOutboxPublisher>();
builder.Services.AddScoped<IConclusionRepository, ConclusionRepository>();
builder.Services.AddScoped<AppealService>();
builder.Services.AddScoped<CompetitionConclusionService>();
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<ApplicationService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = StudentCenter.Security.AccountAccessValidation.Events();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!)),
            ValidateLifetime = true
        };
    });

builder.Services.AddApplicationAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
