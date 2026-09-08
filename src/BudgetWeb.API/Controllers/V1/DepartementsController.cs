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

    [HttpGet("{idDepartement:long}")]
    public async Task<IActionResult> GetById(long idDepartement, CancellationToken cancellationToken)
    {
        var departement = await _departementService.GetByIdAsync(idDepartement, cancellationToken);
        return departement is null ? NotFound() : Ok(departement);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartementRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _departementService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idDepartement = created.IdDepartement }, created);
    }

    [HttpPut("{idDepartement:long}")]
    public async Task<IActionResult> Update(
        long idDepartement,
        [FromBody] UpdateDepartementRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _departementService.UpdateAsync(idDepartement, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idDepartement:long}")]
    public async Task<IActionResult> Delete(long idDepartement, CancellationToken cancellationToken)
    {
        var deleted = await _departementService.DeleteAsync(idDepartement, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
