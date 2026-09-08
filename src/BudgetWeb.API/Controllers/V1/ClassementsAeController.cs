using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/classements-ae")]
public class ClassementsAeController : ControllerBase
{
    private readonly IClassementAeService _service;

    public ClassementsAeController(IClassementAeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] long idVersion,
        [FromQuery] long idUB,
        CancellationToken cancellationToken)
    {
        if (idVersion <= 0 || idUB <= 0)
        {
            return BadRequest(new { message = "idVersion et idUB sont obligatoires." });
        }

        var lignes = await _service.GetAsync(idVersion, idUB, cancellationToken);
        return Ok(lignes);
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(
        [FromBody] ReorderClassementAeRequest request,
        CancellationToken cancellationToken)
    {
        await _service.ReorderAsync(request, cancellationToken);
        var lignes = await _service.GetAsync(request.IdVersion, request.IdUB, cancellationToken);
        return Ok(lignes);
    }

    /// <summary>
    /// Initialise les classements manquants pour les Version×UB ayant des AE.
    /// Idempotent : ne réécrit pas un classement déjà présent.
    /// Ne modifie aucun montant.
    /// </summary>
    [HttpPost("initialiser")]
    public async Task<IActionResult> Initialiser(CancellationToken cancellationToken)
    {
        var result = await _service.InitialiserManquantsAsync(cancellationToken);
        return Ok(result);
    }
}
