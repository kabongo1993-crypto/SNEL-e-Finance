using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/versions-budgetaires")]
public class VersionsBudgetairesController : ControllerBase
{
    private readonly IVersionBudgetaireService _versionBudgetaireService;
    private readonly ICurrentUserService _currentUser;

    public VersionsBudgetairesController(
        IVersionBudgetaireService versionBudgetaireService,
        ICurrentUserService currentUser)
    {
        _versionBudgetaireService = versionBudgetaireService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var versions = await _versionBudgetaireService.GetAllAsync(cancellationToken);
        return Ok(versions);
    }

    [HttpGet("{idVersion:long}")]
    public async Task<IActionResult> GetById(long idVersion, CancellationToken cancellationToken)
    {
        var version = await _versionBudgetaireService.GetByIdAsync(idVersion, cancellationToken);
        return version is null ? NotFound() : Ok(version);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateVersionBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _versionBudgetaireService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idVersion = created.IdVersion }, created);
    }

    [HttpPut("{idVersion:long}")]
    public async Task<IActionResult> Update(
        long idVersion,
        [FromBody] UpdateVersionBudgetaireRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _versionBudgetaireService.UpdateAsync(idVersion, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idVersion:long}")]
    public async Task<IActionResult> Delete(long idVersion, CancellationToken cancellationToken)
    {
        var deleted = await _versionBudgetaireService.DeleteAsync(idVersion, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>BROUILLON|REJETEE → SOUMISE</summary>
    [HttpPost("{idVersion:long}/soumettre")]
    public async Task<IActionResult> Soumettre(long idVersion, CancellationToken cancellationToken)
    {
        var idUtilisateur = _currentUser.RequireUserId();
        var updated = await _versionBudgetaireService.SoumettreAsync(idVersion, idUtilisateur, cancellationToken);
        return Ok(updated);
    }

    /// <summary>SOUMISE → CONTROLEE</summary>
    [HttpPost("{idVersion:long}/controler")]
    public async Task<IActionResult> Controler(long idVersion, CancellationToken cancellationToken)
    {
        var idUtilisateur = _currentUser.RequireUserId();
        var updated = await _versionBudgetaireService.ControlerAsync(idVersion, idUtilisateur, cancellationToken);
        return Ok(updated);
    }

    /// <summary>CONTROLEE → VALIDEE</summary>
    [HttpPost("{idVersion:long}/valider")]
    public async Task<IActionResult> Valider(long idVersion, CancellationToken cancellationToken)
    {
        var idUtilisateur = _currentUser.RequireUserId();
        var updated = await _versionBudgetaireService.ValiderAsync(idVersion, idUtilisateur, cancellationToken);
        return Ok(updated);
    }

    /// <summary>SOUMISE|CONTROLEE → REJETEE</summary>
    [HttpPost("{idVersion:long}/rejeter")]
    public async Task<IActionResult> Rejeter(
        long idVersion,
        [FromBody] RejeterVersionRequest request,
        CancellationToken cancellationToken)
    {
        var idUtilisateur = _currentUser.RequireUserId();
        var updated = await _versionBudgetaireService.RejeterAsync(
            idVersion,
            idUtilisateur,
            request.Motif,
            cancellationToken);
        return Ok(updated);
    }

    /// <summary>REJETEE → BROUILLON (action Modifier)</summary>
    [HttpPost("{idVersion:long}/reouvrir")]
    public async Task<IActionResult> Reouvrir(long idVersion, CancellationToken cancellationToken)
    {
        var idUtilisateur = _currentUser.RequireUserId();
        var updated = await _versionBudgetaireService.ReouvrirAsync(idVersion, idUtilisateur, cancellationToken);
        return Ok(updated);
    }
}
