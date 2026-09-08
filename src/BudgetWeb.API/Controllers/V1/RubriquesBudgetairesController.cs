using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/rubriques-budgetaires")]
public class RubriquesBudgetairesController : ControllerBase
{
    private readonly IRubriqueBudgetaireService _service;

    public RubriquesBudgetairesController(IRubriqueBudgetaireService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var rubriques = await _service.GetAllAsync(cancellationToken);
        return Ok(rubriques);
    }

    [HttpGet("arbre")]
    public async Task<IActionResult> GetArbre(CancellationToken cancellationToken)
    {
        var arbre = await _service.GetArbreAsync(cancellationToken);
        return Ok(arbre);
    }

    [HttpGet("{idRB:long}")]
    public async Task<IActionResult> GetById(long idRB, CancellationToken cancellationToken)
    {
        var rubrique = await _service.GetByIdAsync(idRB, cancellationToken);
        return rubrique is null ? NotFound() : Ok(rubrique);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRubriqueBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idRB = created.IdRB }, created);
    }

    [HttpPut("{idRB:long}")]
    public async Task<IActionResult> Update(
        long idRB,
        [FromBody] UpdateRubriqueBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(idRB, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{idRB:long}/actif")]
    public async Task<IActionResult> SetActif(
        long idRB,
        [FromBody] SetActifRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.SetActifAsync(idRB, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idRB:long}")]
    public async Task<IActionResult> Delete(long idRB, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(idRB, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
