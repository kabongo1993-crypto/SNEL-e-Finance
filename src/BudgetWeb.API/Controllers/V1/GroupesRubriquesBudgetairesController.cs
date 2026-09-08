using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>
/// Référentiel des groupes niveau 1 (ruptures métier) des rubriques budgétaires.
/// </summary>
[ApiController]
[Route("api/v1/groupes-rubriques-budgetaires")]
public class GroupesRubriquesBudgetairesController : ControllerBase
{
    private readonly IGroupeRubriqueBudgetaireService _service;

    public GroupesRubriquesBudgetairesController(IGroupeRubriqueBudgetaireService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var groupes = await _service.GetAllAsync(cancellationToken);
        return Ok(groupes);
    }
}
