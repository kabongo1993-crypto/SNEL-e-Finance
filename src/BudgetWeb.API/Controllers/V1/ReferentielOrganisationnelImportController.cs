using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/import/referentiel-organisationnel")]
public class ReferentielOrganisationnelImportController : ControllerBase
{
    private readonly IReferentielImportService _importService;

    public ReferentielOrganisationnelImportController(IReferentielImportService importService)
    {
        _importService = importService;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromQuery] string? filePath, CancellationToken cancellationToken)
    {
        var result = await _importService.AnalyserAsync(filePath, cancellationToken);
        return Ok(result);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromQuery] string? filePath, CancellationToken cancellationToken)
    {
        var result = await _importService.PrevisualiserAsync(filePath, cancellationToken);
        return Ok(result);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ImportExecuteRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _importService.ExecuterAsync(request, cancellationToken);
        if (!result.Succes)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
