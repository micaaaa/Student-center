using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.BillingService.Application.Services;

namespace StudentCenter.BillingService.API.Controllers;

[ApiController]
[Route("api/billing/me")]
[Authorize(Roles = "STUDENT")]
public sealed class MyChargesController(ChargeService service) : BillingControllerBase
{
    [HttpGet("charges")]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] bool overdueOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.ListMineAsync(overdueOnly, page, pageSize, ct)));
    }

    [HttpGet("charges/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMineAsync(id, ct)));
    }

    [HttpGet("balance")]
    public Task<IActionResult> Balance(CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.MyBalanceAsync(ct)));
    }
}
