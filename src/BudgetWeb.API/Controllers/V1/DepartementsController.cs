using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/departements")]
public class DepartementsController : ControllerBase
{
    private readonly IDepartementService _departementService;

    public DepartementsController(IDepartementService departementService)
    {
        _departementService = departementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var departements = await _departementService.GetAllAsync(cancellationToken);
        return Ok(departements);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartementRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _departementService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = created.IdDepartement }, created);
    }
}
