using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>
/// Journalisation des lectures MCP. Authentification JWT Budget Web obligatoire.
/// Ne journalise jamais de secrets.
/// </summary>
[ApiController]
[Route("api/v1/mcp")]
public sealed class McpJournalController : ControllerBase
{
    private readonly IMcpAuditService _audit;

    public McpJournalController(IMcpAuditService audit)
    {
        _audit = audit;
    }

    [HttpPost("journal")]
    public async Task<IActionResult> Journaliser(
        [FromBody] McpJournalRequest request,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.JournaliserLectureAsync(request, ip, cancellationToken);
        return NoContent();
    }
}
