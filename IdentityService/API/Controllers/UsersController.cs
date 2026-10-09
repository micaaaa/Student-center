using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Application.Exceptions;
using StudentCenter.IdentityService.Application.Interfaces;

namespace StudentCenter.IdentityService.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageUsers")]
public sealed class UsersController(IUserManagementService userManagementService,
    Application.Services.AccountDeletionService deletion) : ControllerBase, IAsyncActionFilter
{
    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        {
            context.Result = Unauthorized();
            return;
        }
        UserResponse actor;
        try { actor = await userManagementService.GetByIdAsync(actorId, context.HttpContext.RequestAborted); }
        catch (NotFoundException) { context.Result = Unauthorized(); return; }
        if (actor.Status != "ACTIVE" || actor.Role is not ("STAFF" or "ADMIN") || !actor.Permissions.Contains("ManageUsers"))
        {
            context.Result = Forbid();
            return;
        }
        if ((HttpMethods.IsPut(context.HttpContext.Request.Method) || HttpMethods.IsDelete(context.HttpContext.Request.Method))
            && context.ActionArguments.TryGetValue("id", out var target) && target is Guid id && id == actorId)
        {
            context.Result = BadRequest(new { message = "You cannot change your own role, permissions or account status." });
            return;
        }
        await next();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var actor = await userManagementService.GetByIdAsync(actorId, ct);
        if (actor.Role != "ADMIN") return Forbid();
        try
        {
            await deletion.DeleteAsync(id, request.Username, ct);
            return NoContent();
        }
        catch (NotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ConflictException exception) { return Conflict(new { message = exception.Message }); }
    }

    public sealed record DeleteAccountRequest(string Username);
    [HttpGet]
    public Task<IReadOnlyCollection<UserResponse>> GetAll(CancellationToken cancellationToken) =>
        userManagementService.GetAllAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await userManagementService.GetByIdAsync(id, cancellationToken));
        }
        catch (NotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserResponse>> UpdateRole(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(() => userManagementService.UpdateRoleAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult<UserResponse>> UpdatePermissions(Guid id, UpdatePermissionsRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(() => userManagementService.UpdatePermissionsAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/activate")]
    public Task<ActionResult<UserResponse>> Activate(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(() => userManagementService.SetAccountStatusAsync(id, true, cancellationToken));

    [HttpPut("{id:guid}/deactivate")]
    public Task<ActionResult<UserResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(() => userManagementService.SetAccountStatusAsync(id, false, cancellationToken));

    private async Task<ActionResult<UserResponse>> ExecuteAsync(Func<Task<UserResponse>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (NotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ConflictException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
