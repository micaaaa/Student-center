using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.BillingService.Application.Services;

namespace StudentCenter.BillingService.API.Controllers;

[ApiController]
[Route("api/billing/me/payments")]
[Authorize(Roles = "STUDENT")]
public sealed class MyPaymentsController(PaymentService service) : BillingControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] Guid? chargeId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.ListMineAsync(chargeId, page, pageSize, ct)));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetMineAsync(id, ct)));
    }
}
