using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/historique-previsions")]
public class HistoriquePrevisionsController : ControllerBase
{
    private readonly IHistoriquePrevisionService _service;

    public HistoriquePrevisionsController(IHistoriquePrevisionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Consultation paginée de l'historique reconstruit depuis JOURNAL_AUDIT
    /// (transitions WORKFLOW_PREVISION_UB). Distinct du statut actuel.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] long? exerciseId,
        [FromQuery] long? idExercice,
        [FromQuery] long? versionId,
        [FromQuery] long? idVersion,
        [FromQuery] long? departementId,
        [FromQuery] long? idDepartement,
        [FromQuery] long? ubId,
        [FromQuery] long? idUB,
        [FromQuery] string? type,
        [FromQuery] string? action,
        [FromQuery] string? statut,
        [FromQuery] DateTime? dateDebut,
        [FromQuery] DateTime? dateFin,
        [FromQuery] string? search,
        [FromQuery] bool? monHistorique,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new HistoriquePrevisionQuery(
            IdExercice: exerciseId ?? idExercice,
            IdVersion: versionId ?? idVersion,
            IdDepartement: departementId ?? idDepartement,
            IdUB: ubId ?? idUB,
            Type: type,
            Action: action,
            Statut: statut,
            DateDebut: dateDebut,
            DateFin: dateFin,
            Search: search,
            MonHistorique: monHistorique ?? true,
            Page: page,
            PageSize: pageSize);

        var result = await _service.QueryAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Timeline Version×UB (tous les événements conservés dans JOURNAL_AUDIT).</summary>
    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline(
        [FromQuery] long idVersion,
        [FromQuery] long idUB,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetTimelineAsync(idVersion, idUB, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
