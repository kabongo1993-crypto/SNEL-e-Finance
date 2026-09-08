using BudgetWeb.Application.SnelComptes.Import.DTOs;
using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/import/snel-comptes")]
public class SnelComptesImportController : ControllerBase
{
    private readonly ISnelComptesImportService _importService;

    public SnelComptesImportController(ISnelComptesImportService importService)
    {
        _importService = importService;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromQuery] string? filePath, CancellationToken cancellationToken)
    {
        var result = await _importService.PrevisualiserAsync(filePath, cancellationToken);
        return Ok(result);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] SnelComptesExecuteRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _importService.ExecuterAsync(request, cancellationToken);
        if (!result.Succes)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
