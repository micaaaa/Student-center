using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Domain.Entities;

namespace StudentCenter.IdentityService.Application.Interfaces;

public interface IStudentRegistrationClient
{
    Task CreateProfileAsync(User user, RegisterRequest request, CancellationToken cancellationToken);
}
