using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/utilisateurs")]
public class UtilisateursController : ControllerBase
{
    private readonly IVersionBudgetaireService _versionBudgetaireService;

    public UtilisateursController(IVersionBudgetaireService versionBudgetaireService)
    {
        _versionBudgetaireService = versionBudgetaireService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var utilisateurs = await _versionBudgetaireService.GetUtilisateursAsync(cancellationToken);
        return Ok(utilisateurs);
    }
}
