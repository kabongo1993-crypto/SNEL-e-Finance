using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/items-bi")]
public class ItemsBIController : ControllerBase
{
    private readonly IItemBIService _service;

    public ItemsBIController(IItemBIService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _service.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("arbre")]
    public async Task<IActionResult> GetArbre(CancellationToken cancellationToken)
    {
        var arbre = await _service.GetArbreAsync(cancellationToken);
        return Ok(arbre);
    }

    [HttpGet("{idItemBI:long}")]
    public async Task<IActionResult> GetById(long idItemBI, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(idItemBI, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateItemBIRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idItemBI = created.IdItemBI }, created);
    }

    [HttpPut("{idItemBI:long}")]
    public async Task<IActionResult> Update(
        long idItemBI,
        [FromBody] UpdateItemBIRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(idItemBI, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{idItemBI:long}/actif")]
    public async Task<IActionResult> SetActif(
        long idItemBI,
        [FromBody] SetActifRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.SetActifAsync(idItemBI, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idItemBI:long}")]
    public async Task<IActionResult> Delete(long idItemBI, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(idItemBI, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
