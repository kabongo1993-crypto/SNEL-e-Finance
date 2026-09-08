using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[AllowAnonymous]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var status = await _healthService.GetStatusAsync(cancellationToken);
        return Ok(new { status = status.Status, database = status.Database });
    }
}
