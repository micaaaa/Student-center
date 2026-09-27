using Microsoft.EntityFrameworkCore; using StudentCenter.ApplicationService.Domain.Entities;
namespace StudentCenter.ApplicationService.Infrastructure.Persistence;
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options):DbContext(options){public DbSet<Competition> Competitions=>Set<Competition>();}
