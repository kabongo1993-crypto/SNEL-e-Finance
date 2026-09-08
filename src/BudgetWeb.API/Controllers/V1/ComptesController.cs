using BudgetWeb.API.Helpers;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Référentiel PCT des comptes financiers (pct.COMPTE).</summary>
[ApiController]
[Route("api/v1/comptes")]
public class ComptesController : ControllerBase
{
    private readonly ICompteFinancierService _service;
    private readonly ICompteCategorieService _categories;

    public ComptesController(ICompteFinancierService service, ICompteCategorieService categories)
    {
        _service = service;
        _categories = categories;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CompteFinancierDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(actifsSeulement, cancellationToken);
            return Ok(rows);
        });

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CompteFinancierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var row = await _service.GetByIdAsync(id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        });

    [HttpPost]
    [ProducesResponseType(typeof(CompteFinancierDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create(
        [FromBody] CreateCompteFinancierRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.IdCompte }, created);
        });

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(CompteFinancierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        long id,
        [FromBody] UpdateCompteFinancierRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });

    [HttpPost("import/preview")]
    [ProducesResponseType(typeof(ImportComptesPreviewDto), StatusCodes.Status200OK)]
    public Task<IActionResult> PreviewImport(
        [FromBody] ImportComptesPreviewRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var preview = await _service.PreviewImportAsync(request, cancellationToken);
            return Ok(preview);
        });

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult Delete(long id)
        => StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            title = "Suppression interdite",
            detail = "La suppression physique d’un compte n’est pas autorisée. Désactivez-le.",
        });

    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportComptesResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Import(
        [FromBody] ImportComptesRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.ImportAsync(request, cancellationToken);
            return Ok(result);
        });

    [HttpGet("{id:long}/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CompteCategorieDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ListCategories(long id, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _categories.ListByCompteAsync(id, cancellationToken);
            return Ok(rows);
        });

    [HttpPost("{id:long}/categories")]
    [ProducesResponseType(typeof(CompteCategorieDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> CreateCategorie(
        long id,
        [FromBody] UpsertCompteCategorieRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _categories.CreateAsync(id, request, cancellationToken);
            return Created($"/api/v1/comptes/{id}/categories/{created.IdCompteCategorie}", created);
        });

    [HttpPut("{id:long}/categories/{relationId:long}")]
    [ProducesResponseType(typeof(CompteCategorieDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateCategorie(
        long id,
        long relationId,
        [FromBody] UpsertCompteCategorieRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _categories.UpdateAsync(id, relationId, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });

    [HttpPost("{id:long}/categories/{relationId:long}/cloturer")]
    [ProducesResponseType(typeof(CompteCategorieDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> CloturerCategorie(
        long id,
        long relationId,
        [FromBody] CloturerCompteCategorieRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _categories.CloturerAsync(id, relationId, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        });

    [HttpDelete("{id:long}/categories/{relationId:long}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult DeleteCategorie(long id, long relationId)
        => StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            title = "Suppression interdite",
            detail = "La suppression physique d’une affectation n’est pas autorisée. Clôturez-la.",
        });

    [HttpPost("categories/import/preview")]
    [ProducesResponseType(typeof(ImportCompteCategoriesPreviewDto), StatusCodes.Status200OK)]
    public Task<IActionResult> PreviewImportCategories(
        [FromBody] ImportCompteCategoriesPreviewRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var preview = await _categories.PreviewImportAsync(request, cancellationToken);
            return Ok(preview);
        });

    [HttpPost("categories/import")]
    [ProducesResponseType(typeof(ImportCompteCategoriesResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> ImportCategories(
        [FromBody] ImportCompteCategoriesRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _categories.ImportAsync(request, cancellationToken);
            return Ok(result);
        });
}
