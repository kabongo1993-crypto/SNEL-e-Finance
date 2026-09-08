using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/structures")]
public class StructuresController : ControllerBase
{
    private readonly IStructureService _structureService;

    public StructuresController(IStructureService structureService)
    {
        _structureService = structureService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var structures = await _structureService.GetAllAsync(cancellationToken);
        return Ok(structures);
    }

    [HttpGet("{idStructure:long}")]
    public async Task<IActionResult> GetById(long idStructure, CancellationToken cancellationToken)
    {
        var structure = await _structureService.GetByIdAsync(idStructure, cancellationToken);
        return structure is null ? NotFound() : Ok(structure);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStructureRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _structureService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idStructure = created.IdStructure }, created);
    }

    [HttpPut("{idStructure:long}")]
    public async Task<IActionResult> Update(
        long idStructure,
        [FromBody] UpdateStructureRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _structureService.UpdateAsync(idStructure, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idStructure:long}")]
    public async Task<IActionResult> Delete(long idStructure, CancellationToken cancellationToken)
    {
        var deleted = await _structureService.DeleteAsync(idStructure, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
