using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/exercices")]
public class ExercicesController : ControllerBase
{
    private readonly IExerciceService _exerciceService;

    public ExercicesController(IExerciceService exerciceService)
    {
        _exerciceService = exerciceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var exercices = await _exerciceService.GetAllAsync(cancellationToken);
        return Ok(exercices);
    }

    [HttpGet("{idExercice:long}")]
    public async Task<IActionResult> GetById(long idExercice, CancellationToken cancellationToken)
    {
        var exercice = await _exerciceService.GetByIdAsync(idExercice, cancellationToken);
        return exercice is null ? NotFound() : Ok(exercice);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateExerciceRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _exerciceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { idExercice = created.IdExercice }, created);
    }

    [HttpPut("{idExercice:long}")]
    public async Task<IActionResult> Update(
        long idExercice,
        [FromBody] UpdateExerciceRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _exerciceService.UpdateAsync(idExercice, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{idExercice:long}")]
    public async Task<IActionResult> Delete(long idExercice, CancellationToken cancellationToken)
    {
        var deleted = await _exerciceService.DeleteAsync(idExercice, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
