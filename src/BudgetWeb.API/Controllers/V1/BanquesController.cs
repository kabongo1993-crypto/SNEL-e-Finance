using BudgetWeb.API.Helpers;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Référentiel PCT des banques (pct.BANQUE).</summary>
[ApiController]
[Route("api/v1/banques")]
public class BanquesController : ControllerBase
{
    private readonly IBanqueService _service;

    public BanquesController(IBanqueService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BanqueDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(actifsSeulement, cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{idBanque}")]
    [ProducesResponseType(typeof(BanqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(string idBanque, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByIdAsync(idBanque, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        });

    [HttpPost]
    [ProducesResponseType(typeof(BanqueDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create(
        [FromBody] CreateBanqueRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { idBanque = created.IdBanque }, created);
        });

    [HttpPut("{idBanque}")]
    [ProducesResponseType(typeof(BanqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        string idBanque,
        [FromBody] UpdateBanqueRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(idBanque, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });

    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportBanquesResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Import(
        [FromBody] ImportBanquesRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.ImportAsync(request, cancellationToken);
            return Ok(result);
        });
}
