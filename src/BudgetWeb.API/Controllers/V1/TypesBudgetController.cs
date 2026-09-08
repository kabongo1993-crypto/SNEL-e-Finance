using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/types-budget")]
public class TypesBudgetController : ControllerBase
{
    private readonly ITypeBudgetService _typeBudgetService;

    public TypesBudgetController(ITypeBudgetService typeBudgetService)
    {
        _typeBudgetService = typeBudgetService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var types = await _typeBudgetService.GetAllAsync(cancellationToken);
        return Ok(types);
    }

    [HttpGet("{idTypeBudget:long}")]
    public async Task<IActionResult> GetById(long idTypeBudget, CancellationToken cancellationToken)
    {
        var type = await _typeBudgetService.GetByIdAsync(idTypeBudget, cancellationToken);
        return type is null ? NotFound() : Ok(type);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTypeBudgetRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _typeBudgetService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idTypeBudget = created.IdTypeBudget }, created);
    }

    [HttpPut("{idTypeBudget:long}")]
    public async Task<IActionResult> Update(
        long idTypeBudget,
        [FromBody] UpdateTypeBudgetRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _typeBudgetService.UpdateAsync(idTypeBudget, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idTypeBudget:long}")]
    public async Task<IActionResult> Delete(long idTypeBudget, CancellationToken cancellationToken)
    {
        var deleted = await _typeBudgetService.DeleteAsync(idTypeBudget, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
