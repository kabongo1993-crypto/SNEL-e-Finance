using BudgetWeb.API.Helpers;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Référentiel PCT des provinces / sites (pct.PROVINCE). Identifiant métier = IdProvince VARCHAR.</summary>
[ApiController]
[Route("api/v1/provinces")]
public class ProvincesController : ControllerBase
{
    private readonly IProvinceService _service;

    public ProvincesController(IProvinceService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProvinceDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(actifsSeulement, cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{idProvince}")]
    [ProducesResponseType(typeof(ProvinceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(string idProvince, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByIdAsync(idProvince, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        });

    [HttpPost]
    [ProducesResponseType(typeof(ProvinceDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create(
        [FromBody] CreateProvinceRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { idProvince = created.IdProvince }, created);
        });

    [HttpPut("{idProvince}")]
    [ProducesResponseType(typeof(ProvinceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        string idProvince,
        [FromBody] UpdateProvinceRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(idProvince, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });
}
