using BudgetWeb.API.Helpers;
using BudgetWeb.API.Infrastructure;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/demandes-paiement/documents-etablis")]
public class DocumentsEtablisController : ControllerBase
{
    private readonly IDocumentsEtablisService _service;

    public DocumentsEtablisController(IDocumentsEtablisService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentEtabliListItemDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] DateOnly dateDebut,
        [FromQuery] DateOnly dateFin,
        [FromQuery] string? typeDocument,
        [FromQuery] long? idExercice,
        [FromQuery] long? idUB,
        [FromQuery] long? idDepartement,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(
                Query(dateDebut, dateFin, typeDocument, idExercice, idUB, idDepartement),
                cancellationToken);
            return Ok(rows);
        });

    [HttpGet("liste-pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> ListePdf(
        [FromQuery] DateOnly dateDebut,
        [FromQuery] DateOnly dateFin,
        [FromQuery] string? typeDocument,
        [FromQuery] long? idExercice,
        [FromQuery] long? idUB,
        [FromQuery] long? idDepartement,
        [FromQuery] string? selection,
        [FromQuery] bool inline = true,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererListePdfAsync(
                Query(dateDebut, dateFin, typeDocument, idExercice, idUB, idDepartement, selection),
                cancellationToken);
            var fileName = $"documents-etablis-{dateDebut:yyyyMMdd}-{dateFin:yyyyMMdd}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpGet("documents-pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> DocumentsPdf(
        [FromQuery] DateOnly dateDebut,
        [FromQuery] DateOnly dateFin,
        [FromQuery] string? typeDocument,
        [FromQuery] long? idExercice,
        [FromQuery] long? idUB,
        [FromQuery] long? idDepartement,
        [FromQuery] string? selection,
        [FromQuery] bool inline = true,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererDocumentsPdfAsync(
                Query(dateDebut, dateFin, typeDocument, idExercice, idUB, idDepartement, selection),
                cancellationToken);
            var fileName = $"documents-etablis-{dateDebut:yyyyMMdd}-{dateFin:yyyyMMdd}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpGet("archive-pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> ArchivePdf(
        [FromQuery] DateOnly dateDebut,
        [FromQuery] DateOnly dateFin,
        [FromQuery] string? typeDocument,
        [FromQuery] long? idExercice,
        [FromQuery] long? idUB,
        [FromQuery] long? idDepartement,
        [FromQuery] string? selection,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var zip = await _service.GenererArchivePdfAsync(
                Query(dateDebut, dateFin, typeDocument, idExercice, idUB, idDepartement, selection),
                cancellationToken);
            var fileName = $"documents-etablis-{dateDebut:yyyyMMdd}-{dateFin:yyyyMMdd}.zip";
            return HttpFileResponses.Attachment(zip, "application/zip", fileName);
        });

    private static DocumentsEtablisQuery Query(
        DateOnly dateDebut,
        DateOnly dateFin,
        string? typeDocument,
        long? idExercice,
        long? idUB,
        long? idDepartement,
        string? selection = null)
        => new(
            dateDebut,
            dateFin,
            typeDocument,
            idExercice,
            idUB,
            idDepartement,
            DocumentEtabliSelectionDto.ParseList(selection));
}
