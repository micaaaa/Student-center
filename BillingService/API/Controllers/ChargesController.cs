using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Services;

namespace StudentCenter.BillingService.API.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize(Policy = "ManageBilling")]
public sealed class ChargesController(ChargeService service) : BillingControllerBase
{
    [HttpPost("charges")]
    public Task<IActionResult> Create(CreateChargeRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId) || actorId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await service.CreateAsync(request, actorId, ct);
            return result.AlreadyExists ? Ok(result.Charge)
                : CreatedAtAction(nameof(Get), new { id = result.Charge.Id }, result.Charge);
        });
    }

    [HttpGet("charges/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetAsync(id, ct)));
    }

    [HttpGet("students/{studentId:guid}/charges")]
    public Task<IActionResult> List(Guid studentId, CancellationToken ct, [FromQuery] bool overdueOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.ListAsync(studentId, overdueOnly, page, pageSize, ct)));
    }

    [HttpGet("students/{studentId:guid}/balance")]
    public Task<IActionResult> Balance(Guid studentId, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.BalanceAsync(studentId, ct)));
    }
}
