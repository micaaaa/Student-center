using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.IdentityService.Application.Exceptions;
using StudentCenter.IdentityService.Application.Interfaces;

namespace StudentCenter.IdentityService.API.Controllers;

[ApiController]
[Route("api/staff-directory")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageMaintenance")]
public sealed class StaffDirectoryController(IUserManagementService users) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        try
        {
            var user = await users.GetByIdAsync(id, ct);
            if (user.Role is not ("STAFF" or "ADMIN"))
            {
                return NotFound(new { message = "Staff account not found." });
            }

            return Ok(new { user.Id, user.Role, user.Status });
        }
        catch (NotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }
}
