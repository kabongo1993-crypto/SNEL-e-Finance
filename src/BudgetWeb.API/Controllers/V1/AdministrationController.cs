using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>
/// Administration des utilisateurs, profils et permissions.
/// Ne remplace pas GET /api/v1/utilisateurs (lookup versions).
/// </summary>
[ApiController]
[Route("api/v1/administration")]
public class AdministrationController : ControllerBase
{
    private readonly IUtilisateurAdminService _service;

    public AdministrationController(IUtilisateurAdminService service)
    {
        _service = service;
    }

    [HttpGet("utilisateurs")]
    public async Task<IActionResult> ListUtilisateurs(CancellationToken cancellationToken)
    {
        var items = await _service.ListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("utilisateurs/{idUtilisateur:long}")]
    public async Task<IActionResult> GetUtilisateur(long idUtilisateur, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(idUtilisateur, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("utilisateurs")]
    public async Task<IActionResult> CreateUtilisateur(
        [FromBody] CreateUtilisateurAdminRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUtilisateur), new { idUtilisateur = created.IdUtilisateur }, created);
    }

    [HttpPut("utilisateurs/{idUtilisateur:long}")]
    public async Task<IActionResult> UpdateUtilisateur(
        long idUtilisateur,
        [FromBody] UpdateUtilisateurAdminRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(idUtilisateur, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPatch("utilisateurs/{idUtilisateur:long}/actif")]
    public async Task<IActionResult> SetActif(
        long idUtilisateur,
        [FromBody] SetActifUtilisateurRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.SetActifAsync(idUtilisateur, request.Actif, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("utilisateurs/{idUtilisateur:long}/reset-mot-de-passe")]
    public async Task<IActionResult> ResetMotDePasse(
        long idUtilisateur,
        [FromBody] ResetMotDePasseAdminRequest request,
        CancellationToken cancellationToken)
    {
        await _service.ResetMotDePasseAsync(idUtilisateur, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Consultation seule : profil → permissions héritées (+ catalogue permissions).</summary>
    [HttpGet("profils")]
    public async Task<IActionResult> GetCatalogue(CancellationToken cancellationToken)
    {
        var catalogue = await _service.GetCatalogueAsync(cancellationToken);
        return Ok(catalogue);
    }
}
