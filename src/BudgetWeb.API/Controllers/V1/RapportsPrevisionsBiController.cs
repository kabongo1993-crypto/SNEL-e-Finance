using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/rapports/previsions-bi")]
public class RapportsPrevisionsBiController : ControllerBase
{
    private readonly IRapportPrevisionBiService _service;

    public RapportsPrevisionsBiController(IRapportPrevisionBiService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] long idVersion,
        [FromQuery] string niveau,
        [FromQuery] long? idEntite,
        [FromQuery] long? idDepartementStructure,
        [FromQuery] long? idDivision,
        [FromQuery] long? idUB,
        [FromQuery] string? statutConsultation,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _service.GetAsync(
                new RapportBiQuery(idVersion, niveau, idEntite, idDepartementStructure, idDivision, idUB, statutConsultation),
                cancellationToken);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(
        [FromQuery] long idVersion,
        [FromQuery] string niveau,
        [FromQuery] long? idEntite,
        [FromQuery] long? idDepartementStructure,
        [FromQuery] long? idDivision,
        [FromQuery] long? idUB,
        [FromQuery] string? statutConsultation,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new RapportBiQuery(idVersion, niveau, idEntite, idDepartementStructure, idDivision, idUB, statutConsultation);
            var dto = await _service.GetAsync(query, cancellationToken);
            var bytes = await _service.GetPdfAsync(query, cancellationToken);
            var fileName = dto.EnTete.ReferenceDocument.Replace('/', '_') + ".pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
