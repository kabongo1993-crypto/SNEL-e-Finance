using BudgetWeb.API.Helpers;
using BudgetWeb.API.Models;
using BudgetWeb.Application.DTOs.Referentiels;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Application.Referentiels;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Taux de change — référentiel central transversal (dpm.TAUX_CHANGE).</summary>
[ApiController]
[Route("api/v1/taux-change")]
public class TauxChangeController : ControllerBase
{
    private readonly ITauxChangeService _service;

    public TauxChangeController(ITauxChangeService service)
    {
        _service = service;
    }

    [HttpGet("paires")]
    [ProducesResponseType(typeof(IReadOnlyList<PaireTauxChangeDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> ListPaires(CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
            Ok(await _service.ListerPairesSupporteesAsync(cancellationToken)));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TauxChangeDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] TauxChangeListQuery query,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(query, cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(TauxChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByIdAsync(id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        });

    [HttpGet("applicable")]
    [ProducesResponseType(typeof(TauxChangeApplicableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetApplicable(
        [FromQuery] string deviseSource,
        [FromQuery] string deviseCible,
        [FromQuery] DateOnly dateReference,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var taux = await _service.GetApplicableAsync(deviseSource, deviseCible, dateReference, cancellationToken);
            return taux is null ? NotFound() : Ok(taux);
        });

    [HttpPost("convertir")]
    [ProducesResponseType(typeof(ConversionResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Convertir(
        [FromBody] ConvertirTauxChangeRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.ConvertirAsync(
                request.MontantSource,
                request.DeviseSource,
                request.DeviseCible,
                request.DateReference,
                cancellationToken);
            return Ok(result);
        });

    [HttpPost]
    [ProducesResponseType(typeof(TauxChangeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateVersion(
        [FromBody] CreateTauxChangeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateVersionAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.IdTauxChange }, created);
        }
        catch (TauxChangeRemplacementRequisException ex)
        {
            return new ObjectResult(new ApiErrorResponse
            {
                Status = StatusCodes.Status409Conflict,
                Message = ex.Message,
                Code = ex.Proposition.Code,
                Remplacement = ex.Proposition,
            })
            {
                StatusCode = StatusCodes.Status409Conflict,
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return DemandePaiementApiResults.Status(401, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return DemandePaiementApiResults.Status(400, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return DemandePaiementApiResults.Status(400, ex.Message);
        }
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(TauxChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateVersion(
        long id,
        [FromBody] UpdateTauxChangeRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateVersionAsync(id, request, cancellationToken);
            return Ok(updated);
        });

    [HttpPost("{id:long}/inactivate")]
    [ProducesResponseType(typeof(TauxChangeDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Inactivate(
        long id,
        [FromBody] InactivateTauxChangeRequest? request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.InactivateAsync(id, request ?? new InactivateTauxChangeRequest(), cancellationToken);
            return Ok(updated);
        });
}
