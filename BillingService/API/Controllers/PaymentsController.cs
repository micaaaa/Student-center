using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.BillingService.Application.DTOs;
using StudentCenter.BillingService.Application.Services;

namespace StudentCenter.BillingService.API.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize(Policy = "ManageBilling")]
public sealed class PaymentsController(PaymentService service) : BillingControllerBase
{
    [HttpPost("payments")]
    public Task<IActionResult> Record(RecordPaymentRequest request, CancellationToken ct)
    {
        return ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId) || actorId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await service.RecordAsync(request, actorId, ct);
            return result.AlreadyExists ? Ok(result.Payment)
                : CreatedAtAction(nameof(Get), new { id = result.Payment.Id }, result.Payment);
        });
    }

    [HttpGet("payments/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(async () => Ok(await service.GetAsync(id, ct)));
    }

    [HttpGet("students/{studentId:guid}/payments")]
    public Task<IActionResult> List(Guid studentId, CancellationToken ct, [FromQuery] Guid? chargeId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return ExecuteAsync(async () => Ok(await service.ListAsync(studentId, chargeId, page, pageSize, ct)));
    }
}
