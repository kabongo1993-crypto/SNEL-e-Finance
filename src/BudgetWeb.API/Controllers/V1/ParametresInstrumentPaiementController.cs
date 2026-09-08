using BudgetWeb.API.Helpers;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Paramétrage comptable des documents instrument de paiement DPM.</summary>
[ApiController]
[Route("api/v1/parametres-instrument-paiement")]
public class ParametresInstrumentPaiementController : ControllerBase
{
    private readonly IParametreInstrumentPaiementService _service;

    public ParametresInstrumentPaiementController(IParametreInstrumentPaiementService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ParametreInstrumentPaiementDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{typeInstrument}")]
    [ProducesResponseType(typeof(ParametreInstrumentPaiementDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetByType(string typeInstrument, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByTypeAsync(typeInstrument, cancellationToken);
            return Ok(row);
        });

    [HttpPut("{typeInstrument}")]
    [ProducesResponseType(typeof(ParametreInstrumentPaiementDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Upsert(
        string typeInstrument,
        [FromBody] UpsertParametreInstrumentRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var saved = await _service.UpsertAsync(typeInstrument, request, cancellationToken);
            return Ok(saved);
        });
}
