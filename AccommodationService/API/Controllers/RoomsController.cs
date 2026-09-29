using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Services;

namespace StudentCenter.AccommodationService.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageAccommodation")]
public sealed class RoomsController(InventoryService service) : InventoryControllerBase
{
    [HttpGet("dorms/{dormId:guid}/rooms")]
    public Task<IActionResult> List(Guid dormId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetRoomsAsync(dormId, ct)));

    [HttpGet("rooms/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetRoomAsync(id, ct)));

    [HttpPost("dorms/{dormId:guid}/rooms")]
    public Task<IActionResult> Create(Guid dormId, RoomRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await service.CreateRoomAsync(dormId, request, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });

    [HttpPut("rooms/{id:guid}")]
    public Task<IActionResult> Update(Guid id, RoomRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.UpdateRoomAsync(id, request, ct)));
}
