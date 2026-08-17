using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/referentiels")]
public class ReferentielsController : ControllerBase
{
    private readonly IReferentielService _referentielService;

    public ReferentielsController(IReferentielService referentielService)
    {
        _referentielService = referentielService;
    }

    [HttpGet("types-budget")]
    public async Task<IActionResult> GetTypesBudget(CancellationToken cancellationToken)
    {
        var types = await _referentielService.GetTypesBudgetAsync(cancellationToken);
        return Ok(types);
    }

    [HttpGet("modes-prevision")]
    public async Task<IActionResult> GetModesPrevision(CancellationToken cancellationToken)
    {
        var modes = await _referentielService.GetModesPrevisionAsync(cancellationToken);
        return Ok(modes);
    }
}
