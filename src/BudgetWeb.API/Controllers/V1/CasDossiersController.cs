using BudgetWeb.API.Helpers;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Référentiel des cas de dossier DPM.</summary>
[ApiController]
[Route("api/v1/cas-dossiers")]
public class CasDossiersController : ControllerBase
{
    private readonly ICasDossierService _service;

    public CasDossiersController(ICasDossierService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CasDossierDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] bool actifsSeulement = true,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(actifsSeulement, cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{idCasDossier:long}")]
    [ProducesResponseType(typeof(CasDossierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(
        long idCasDossier,
        [FromQuery] bool piecesActivesSeulement = true,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByIdAsync(idCasDossier, piecesActivesSeulement, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        });

    [HttpPost]
    [ProducesResponseType(typeof(CasDossierDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create(
        [FromBody] CreateCasDossierRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { idCasDossier = created.IdCasDossier }, created);
        });

    [HttpPut("{idCasDossier:long}")]
    [ProducesResponseType(typeof(CasDossierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        long idCasDossier,
        [FromBody] UpdateCasDossierRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(idCasDossier, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });

    [HttpPost("{idCasDossier:long}/pieces")]
    [ProducesResponseType(typeof(CasDossierPieceObligatoireDto), StatusCodes.Status201Created)]
    public Task<IActionResult> AddPiece(
        long idCasDossier,
        [FromBody] CreateCasDossierPieceRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.AddPieceAsync(idCasDossier, request, cancellationToken);
            return CreatedAtAction(
                nameof(GetById),
                new { idCasDossier, piecesActivesSeulement = false },
                created);
        });

    [HttpPut("{idCasDossier:long}/pieces/{idPiece:long}")]
    [ProducesResponseType(typeof(CasDossierPieceObligatoireDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdatePiece(
        long idCasDossier,
        long idPiece,
        [FromBody] UpdateCasDossierPieceRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdatePieceAsync(idCasDossier, idPiece, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });
}
