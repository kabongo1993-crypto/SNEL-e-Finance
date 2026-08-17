using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/referentiel-organisationnel")]
public class ReferentielOrganisationnelController : ControllerBase
{
    private readonly IReferentielOrganisationnelQueryService _queryService;

    public ReferentielOrganisationnelController(IReferentielOrganisationnelQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// Snapshot lecture seule du référentiel organisationnel (structures + UB) pour l'écran d'exploration.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = await _queryService.GetSnapshotAsync(cancellationToken);
        return Ok(snapshot);
    }
}
