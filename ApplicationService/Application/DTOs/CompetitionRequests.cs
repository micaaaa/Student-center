using System.ComponentModel.DataAnnotations;
namespace StudentCenter.ApplicationService.Application.DTOs;
public sealed class CreateCompetitionRequest { [Required,MaxLength(20)] public string AcademicYear {get;init;}=string.Empty; [Required,MaxLength(200)] public string Name {get;init;}=string.Empty; [MaxLength(2000)] public string? Description {get;init;} [Required] public DateTime ApplicationStartDateUtc {get;init;} [Required] public DateTime ApplicationEndDateUtc {get;init;} }
public sealed class UpdateCompetitionRequest { [Required,MaxLength(200)] public string Name {get;init;}=string.Empty; [MaxLength(2000)] public string? Description {get;init;} [Required] public DateTime ApplicationStartDateUtc {get;init;} [Required] public DateTime ApplicationEndDateUtc {get;init;} }
public sealed record CompetitionResponse(Guid Id,string AcademicYear,string Name,string? Description,DateTime ApplicationStartDateUtc,DateTime ApplicationEndDateUtc,string Status);
