using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Services;

namespace StudentCenter.AccommodationService.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageAccommodation")]
public sealed class AssignmentsController(AssignmentService service) : InventoryControllerBase
{
    [HttpGet("competitions/{competitionId:guid}/received-eligibilities")]
    public Task<IActionResult> Eligibilities(Guid competitionId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetEligibilitiesAsync(competitionId, ct)));

    [HttpPost("accommodations")]
    public Task<IActionResult> Assign(AssignRoomRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)
                || staffId == Guid.Empty)
                return Unauthorized();
            var result = await service.AssignAsync(request, staffId, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });

    [HttpGet("accommodations/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetAsync(id, ct)));

    [HttpGet("students/{studentId:guid}/accommodations")]
    public Task<IActionResult> History(Guid studentId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetHistoryAsync(studentId, ct)));

    [HttpPost("accommodations/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancelAssignmentRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)
                || staffId == Guid.Empty)
                return Unauthorized();
            return Ok(await service.CancelAsync(id, request.Reason, staffId, ct));
        });

    [HttpPost("accommodations/{id:guid}/move-in")]
    public Task<IActionResult> MoveIn(Guid id, MoveInRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)
                || staffId == Guid.Empty)
                return Unauthorized();
            return Ok(await service.MoveInAsync(id, request, staffId, ct));
        });

    [HttpPost("accommodations/{id:guid}/move-out")]
    public Task<IActionResult> MoveOut(Guid id, MoveOutRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)
                || staffId == Guid.Empty)
                return Unauthorized();
            return Ok(await service.MoveOutAsync(id, request, staffId, ct));
        });
}
