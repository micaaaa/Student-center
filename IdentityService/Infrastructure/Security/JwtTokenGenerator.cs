using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Application.Interfaces;
using StudentCenter.IdentityService.Domain.Entities;

namespace StudentCenter.IdentityService.Infrastructure.Security;

public sealed class JwtTokenGenerator(IOptions<JwtSettings> jwtOptions) : IJwtTokenGenerator
{
    public string CreateAccessToken(User user)
    {
        var settings = jwtOptions.Value;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(settings.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToUpperInvariant())
        }.Concat(user.Permissions.Select(permission => new Claim("permission", permission.Permission.ToString())));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: expiresAtUtc, signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
