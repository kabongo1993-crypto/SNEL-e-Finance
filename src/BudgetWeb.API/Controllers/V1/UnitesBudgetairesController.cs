using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/unites-budgetaires")]
public class UnitesBudgetairesController : ControllerBase
{
    private readonly IUniteBudgetaireService _uniteBudgetaireService;

    public UnitesBudgetairesController(IUniteBudgetaireService uniteBudgetaireService)
    {
        _uniteBudgetaireService = uniteBudgetaireService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool accessibles = false,
        [FromQuery] string? contexte = null,
        CancellationToken cancellationToken = default)
    {
        var unites = await _uniteBudgetaireService.GetAllAsync(accessibles, contexte, cancellationToken);
        return Ok(unites);
    }

    [HttpGet("{idUb:long}")]
    public async Task<IActionResult> GetById(long idUb, CancellationToken cancellationToken)
    {
        var unite = await _uniteBudgetaireService.GetByIdAsync(idUb, cancellationToken);
        return unite is null ? NotFound() : Ok(unite);
    }

    [HttpPut("{idUb:long}")]
    public async Task<IActionResult> Update(
        long idUb,
        [FromBody] UpdateUniteBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _uniteBudgetaireService.UpdateAsync(idUb, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUniteBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _uniteBudgetaireService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idUb = created.IdUB }, created);
    }

    [HttpDelete("{idUb:long}")]
    public async Task<IActionResult> Delete(long idUb, CancellationToken cancellationToken)
    {
        var deleted = await _uniteBudgetaireService.DeleteAsync(idUb, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
