using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCenter.AccommodationService.Application.DTOs;
using StudentCenter.AccommodationService.Application.Services;

namespace StudentCenter.AccommodationService.API.Controllers;

[ApiController]
[Route("api/dorms")]
[Authorize(Roles = "STAFF,ADMIN", Policy = "ManageAccommodation")]
public sealed class DormsController(InventoryService service) : InventoryControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetDormsAsync(ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.GetDormAsync(id, ct)));

    [HttpPost]
    public Task<IActionResult> Create(DormRequest request, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await service.CreateDormAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, DormRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await service.UpdateDormAsync(id, request, ct)));
}
