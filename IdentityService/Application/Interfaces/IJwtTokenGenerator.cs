using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Domain.Entities;

namespace StudentCenter.IdentityService.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string CreateAccessToken(User user);
}
