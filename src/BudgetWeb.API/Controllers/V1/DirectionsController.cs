using BudgetWeb.API.Helpers;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Référentiel PCT des directions / implantations (pct.DIRECTION). Identifiant = IdDirection IDENTITY.</summary>
[ApiController]
[Route("api/v1/directions")]
public class DirectionsController : ControllerBase
{
    private readonly IDirectionService _service;

    public DirectionsController(IDirectionService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DirectionDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(actifsSeulement, cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(DirectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByIdAsync(id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        });

    [HttpPost]
    [ProducesResponseType(typeof(DirectionDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create(
        [FromBody] CreateDirectionRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.IdDirection }, created);
        });

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(DirectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        long id,
        [FromBody] UpdateDirectionRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });
}
