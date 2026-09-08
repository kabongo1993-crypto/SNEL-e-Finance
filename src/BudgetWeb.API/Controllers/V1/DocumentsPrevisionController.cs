using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/documents-prevision")]
public class DocumentsPrevisionController : ControllerBase
{
    private readonly IDocumentPrevisionService _service;

    public DocumentsPrevisionController(IDocumentPrevisionService service)
    {
        _service = service;
    }

    [HttpGet("{idDocument:long}")]
    public async Task<IActionResult> GetById(long idDocument, CancellationToken cancellationToken)
    {
        var doc = await _service.GetByIdAsync(idDocument, cancellationToken);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpGet("reference/{*reference}")]
    public async Task<IActionResult> GetByReference(string reference, CancellationToken cancellationToken)
    {
        var decoded = Uri.UnescapeDataString(reference ?? string.Empty);
        var doc = await _service.GetByReferenceAsync(decoded, cancellationToken);
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
        var meta = await _service.GetByIdAsync(idDocument, cancellationToken);
        if (meta is null) return NotFound();
        var bytes = await _service.GetPdfBytesAsync(idDocument, cancellationToken);
        if (bytes is null || bytes.Length == 0) return NotFound();
        var fileName = meta.Reference.Replace('/', '_') + ".pdf";
        return File(bytes, "application/pdf", fileName);
    }
}
