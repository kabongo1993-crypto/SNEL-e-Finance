using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/previsions-budgetaires")]
public class PrevisionsBudgetairesController : ControllerBase
{
    private readonly IPrevisionBudgetaireService _service;
    private readonly ICurrentUserService _currentUser;

    public PrevisionsBudgetairesController(
        IPrevisionBudgetaireService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetByFiltres(
        [FromQuery] long? idVersion,
        [FromQuery] long? idTypeBudget,
        [FromQuery] long? idUB,
        [FromQuery] long? idModePrevision,
        [FromQuery] string? libelleItemAE,
        [FromQuery] long? idItemBI,
        CancellationToken cancellationToken)
    {
        var items = await _service.GetByFiltresAsync(
            idVersion, idTypeBudget, idUB, idModePrevision, libelleItemAE, idItemBI, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{idPrevision:long}")]
    public async Task<IActionResult> GetById(long idPrevision, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(idPrevision, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("grille")]
    public async Task<IActionResult> GetGrille(
        [FromQuery] long idVersion,
        [FromQuery] long idTypeBudget,
        [FromQuery] long idModePrevision,
        [FromQuery] long idUB,
        [FromQuery] string? libelleItemAE,
        [FromQuery] long? idGroupeItemAE,
        [FromQuery] long? idItemBI,
        CancellationToken cancellationToken)
    {
        var grille = await _service.GetGrilleAsync(
            idVersion, idTypeBudget, idModePrevision, idUB, libelleItemAE, idGroupeItemAE, idItemBI, cancellationToken);
        return Ok(grille);
    }

    [HttpGet("grille-page")]
    public async Task<IActionResult> GetGrillePage(
        [FromQuery] long idVersion,
        [FromQuery] long idTypeBudget,
        [FromQuery] long idModePrevision,
        [FromQuery] long idUB,
        [FromQuery] string? libelleItemAE,
        [FromQuery] long? idGroupeItemAE,
        [FromQuery] long? idItemBI,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? search = null,
        [FromQuery] string? filtre = null,
        [FromQuery] long? idGroupeRB = null,
        CancellationToken cancellationToken = default)
    {
        var grille = await _service.GetGrillePageAsync(
            idVersion, idTypeBudget, idModePrevision, idUB, libelleItemAE, idGroupeItemAE, idItemBI,
            page, pageSize, search, filtre, idGroupeRB, cancellationToken);
        return Ok(grille);
    }

    [HttpGet("resume")]
    public async Task<IActionResult> GetResume([FromQuery] long idVersion, CancellationToken cancellationToken)
    {
        var resume = await _service.GetResumeAsync(idVersion, cancellationToken);
        return Ok(resume);
    }

    /// <summary>
    /// Liste distincte des actions d'exploitation déjà saisies (réutilisation multi-UB).
    /// Filtrer par exercice (recommandé) et/ou version.
    /// </summary>
    [HttpGet("actions-ae")]
    public async Task<IActionResult> ListActionsAE(
        [FromQuery] long? idVersion,
        [FromQuery] long? idExercice,
        CancellationToken cancellationToken)
    {
        if (idVersion is not > 0 && idExercice is not > 0)
        {
            return BadRequest(new { message = "Indiquez idExercice et/ou idVersion." });
        }

        var actions = await _service.ListLibellesItemAEAsync(idVersion, idExercice, cancellationToken);
        return Ok(actions);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePrevisionBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var normalized = request with { IdUtilisateurCreation = userId };
        var created = await _service.CreateAsync(normalized, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idPrevision = created.IdPrevision }, created);
    }

    [HttpPut("{idPrevision:long}")]
    public async Task<IActionResult> Update(
        long idPrevision,
        [FromBody] UpdatePrevisionBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var normalized = request with { IdUtilisateurModification = userId };
        var updated = await _service.UpdateAsync(idPrevision, normalized, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idPrevision:long}")]
    public async Task<IActionResult> Delete(long idPrevision, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(idPrevision, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("sauvegarder-grille")]
    public async Task<IActionResult> SauvegarderGrille(
        [FromBody] SauvegarderGrillePrevisionRequest request,
        CancellationToken cancellationToken)
    {
        // IdUtilisateur du body est ignoré : CurrentUser.UserId est la seule source de vérité.
        var userId = _currentUser.RequireUserId();
        var normalized = request with { IdUtilisateur = userId };
        var result = await _service.SauvegarderGrilleAsync(normalized, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/v1/groupes-item-ae")]
public class GroupesItemAEController : ControllerBase
{
    private readonly IGroupeItemAEService _service;

    public GroupesItemAEController(IGroupeItemAEService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{idGroupeItemAE:long}")]
    public async Task<IActionResult> GetById(long idGroupeItemAE, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(idGroupeItemAE, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupeItemAERequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idGroupeItemAE = created.IdGroupeItemAE }, created);
    }

    [HttpPut("{idGroupeItemAE:long}")]
    public async Task<IActionResult> Update(
        long idGroupeItemAE,
        [FromBody] UpdateGroupeItemAERequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(idGroupeItemAE, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idGroupeItemAE:long}")]
    public async Task<IActionResult> Delete(long idGroupeItemAE, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(idGroupeItemAE, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
