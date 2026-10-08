using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using StudentCenter.ApplicationService.Infrastructure.Security;

namespace StudentCenter.ApplicationService.Tests;

[TestFixture]
public sealed class AuthorizationTests
{
    [TestCase("STUDENT", true, true, false)]
    [TestCase("STAFF", false, true, false)]
    [TestCase("STAFF", true, true, true)]
    [TestCase("ADMIN", true, true, true)]
    [TestCase("STAFF", true, false, false)]
    public async Task CompetitionManagementRequiresAuthenticatedStaffWithPermission(
        string role, bool hasPermission, bool authenticated, bool allowed)
    {
        using var services = new ServiceCollection().AddLogging()
            .AddApplicationAuthorization().BuildServiceProvider();
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (hasPermission)
        {
            claims.Add(new Claim("permission", "ManageApplications"));
        }
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Test" : null));
        var result = await services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, null, "ManageApplications");
        Assert.That(result.Succeeded, Is.EqualTo(allowed));
    }
}
