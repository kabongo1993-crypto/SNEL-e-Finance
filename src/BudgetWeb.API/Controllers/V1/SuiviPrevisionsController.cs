using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/suivi-previsions")]
public class SuiviPrevisionsController : ControllerBase
{
    private readonly ISuiviPrevisionService _service;

    public SuiviPrevisionsController(ISuiviPrevisionService service)
    {
        _service = service;
    }

    /// <summary>Prévisions préparées par l'utilisateur connecté (IdUtilisateurCreation).</summary>
    [HttpGet("mes-previsions")]
    public async Task<IActionResult> GetMesPrevisions(
        [FromQuery] long? idExercice,
        [FromQuery] long? idVersion,
        [FromQuery] long? idDepartement,
        [FromQuery] string? statut,
        [FromQuery] string? searchUb,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetMesPrevisionsAsync(
            idExercice, idVersion, idDepartement, statut, searchUb, cancellationToken);
        return Ok(result);
    }

    /// <summary>Soumissions Version×UB (hors brouillon par défaut). Permissions contrôle/validation/rejet.</summary>
    [HttpGet("soumissions")]
    public async Task<IActionResult> GetSoumissions(
        [FromQuery] long? idExercice,
        [FromQuery] long? idVersion,
        [FromQuery] long? idDepartement,
        [FromQuery] string? statut,
        [FromQuery] string? searchUb,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetSoumissionsAsync(
            idExercice, idVersion, idDepartement, statut, searchUb, cancellationToken);
        return Ok(result);
    }

    /// <summary>En-tête + totaux DC/AE/BI pour une Version×UB (sans charger toutes les RB).</summary>
    [HttpGet("ub-detail")]
    public async Task<IActionResult> GetUbDetail(
        [FromQuery] long idVersion,
        [FromQuery] long idUB,
        CancellationToken cancellationToken)
    {
        var detail = await _service.GetUbDetailAsync(idVersion, idUB, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Détail lignes DC, AE ou BI (chargé à l'ouverture d'un onglet).</summary>
    [HttpGet("ub-detail/lignes")]
    public async Task<IActionResult> GetUbLignes(
        [FromQuery] long idVersion,
        [FromQuery] long idUB,
        [FromQuery] string codeType,
        CancellationToken cancellationToken)
    {
        var lignes = await _service.GetUbLignesAsync(idVersion, idUB, codeType, cancellationToken);
        return Ok(lignes);
    }
}
