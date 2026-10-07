using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StudentCenter.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NotificationServiceKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["InternalServices:NotificationKey"];
        var supplied = context.HttpContext.Request.Headers["X-Notification-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32 || supplied.Length > 256
            || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
