using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/ajustements-budgetaires")]
public class AjustementsBudgetairesController : ControllerBase
{
    private readonly IAjustementBudgetaireService _service;

    public AjustementsBudgetairesController(IAjustementBudgetaireService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? idExercice,
        [FromQuery] long? idVersion,
        [FromQuery] long? idUB,
        [FromQuery] string? statut,
        CancellationToken cancellationToken)
    {
        var rows = await _service.GetAllAsync(
            new AjustementBudgetaireQuery(idExercice, idVersion, idUB, statut),
            cancellationToken);
        return Ok(rows);
    }

    [HttpGet("{idAjustement:long}")]
    public async Task<IActionResult> GetById(long idAjustement, CancellationToken cancellationToken)
    {
        var row = await _service.GetByIdAsync(idAjustement, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("historique-ligne")]
    public async Task<IActionResult> HistoriqueLigne(
        [FromQuery] long idPrevision,
        CancellationToken cancellationToken)
    {
        var histo = await _service.GetHistoriqueLigneAsync(idPrevision, cancellationToken);
        return Ok(histo);
    }

    [HttpGet("lignes-validees")]
    public async Task<IActionResult> LignesValidees(
        [FromQuery] long idVersion,
        [FromQuery] long idUB,
        CancellationToken cancellationToken)
    {
        var lignes = await _service.GetLignesValideesAsync(idVersion, idUB, cancellationToken);
        return Ok(lignes);
    }

    [HttpGet("lignes-disponibles")]
    public async Task<IActionResult> LignesDisponibles(
        [FromQuery] long? idExercice,
        [FromQuery] long? idVersion,
        [FromQuery] long? idUB,
        CancellationToken cancellationToken)
    {
        var lignes = await _service.GetLignesDisponiblesAsync(
            new AjustementBudgetaireQuery(idExercice, idVersion, idUB, null),
            cancellationToken);
        return Ok(lignes);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAjustementBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idAjustement = created.IdAjustement }, created);
    }

    [HttpPut("{idAjustement:long}")]
    public async Task<IActionResult> Update(
        long idAjustement,
        [FromBody] UpdateAjustementBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(idAjustement, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{idAjustement:long}/valider")]
    public async Task<IActionResult> Valider(long idAjustement, CancellationToken cancellationToken)
    {
        var updated = await _service.ValiderAsync(idAjustement, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{idAjustement:long}/annuler")]
    public async Task<IActionResult> Annuler(long idAjustement, CancellationToken cancellationToken)
    {
        var updated = await _service.AnnulerAsync(idAjustement, cancellationToken);
        return Ok(updated);
    }
}
