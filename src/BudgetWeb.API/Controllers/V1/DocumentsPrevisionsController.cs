using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/documents-previsions")]
public class DocumentsPrevisionsController : ControllerBase
{
    private readonly IDocumentPrevisionService _service;

    public DocumentsPrevisionsController(IDocumentPrevisionService service)
    {
        _service = service;
    }

    [HttpGet("{idDocument:long}")]
    public async Task<IActionResult> GetById(long idDocument, CancellationToken cancellationToken)
    {
        var doc = await _service.GetByIdAsync(idDocument, cancellationToken);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpGet("by-reference")]
    public async Task<IActionResult> GetByReference([FromQuery] string reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequest(new { message = "La référence est obligatoire." });
        }

        var doc = await _service.GetByReferenceAsync(reference, cancellationToken);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpGet("by-audit/{idAudit:long}")]
    public async Task<IActionResult> GetByAudit(long idAudit, CancellationToken cancellationToken)
    {
        var doc = await _service.GetByAuditAsync(idAudit, cancellationToken);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpGet("{idDocument:long}/pdf")]
    public async Task<IActionResult> DownloadPdf(long idDocument, CancellationToken cancellationToken)
    {
        var doc = await _service.GetByIdAsync(idDocument, cancellationToken);
        if (doc is null)
        {
            return NotFound();
        }

        var bytes = await _service.GetPdfBytesAsync(idDocument, cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return NotFound(new { message = "Fichier PDF introuvable." });
        }

        var fileName = doc.Reference.Replace('/', '_') + ".pdf";
        return File(bytes, "application/pdf", fileName);
    }
}
