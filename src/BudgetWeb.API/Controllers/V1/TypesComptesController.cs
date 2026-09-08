using BudgetWeb.API.Helpers;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Référentiel PCT des types de comptes (pct.TYPE_COMPTE). Identifiant = Code.</summary>
[ApiController]
[Route("api/v1/types-comptes")]
public class TypesComptesController : ControllerBase
{
    private readonly ITypeCompteService _service;

    public TypesComptesController(ITypeCompteService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TypeCompteDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(actifsSeulement, cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(TypeCompteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByCodeAsync(code, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        });

    [HttpPost]
    [ProducesResponseType(typeof(TypeCompteDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create(
        [FromBody] CreateTypeCompteRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
        });

    [HttpPut("{code}")]
    [ProducesResponseType(typeof(TypeCompteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        string code,
        [FromBody] UpdateTypeCompteRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(code, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });
}
