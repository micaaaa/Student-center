using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.IdentityService.Application.DTOs;
using StudentCenter.IdentityService.Application.Exceptions;
using StudentCenter.IdentityService.Application.Interfaces;

namespace StudentCenter.IdentityService.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "ManageUsers")]
public sealed class UsersController(IUserManagementService userManagementService) : ControllerBase
{
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
