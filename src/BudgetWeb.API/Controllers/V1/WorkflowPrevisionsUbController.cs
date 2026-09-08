using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Route("api/v1/workflow-previsions-ub")]
public class WorkflowPrevisionsUbController : ControllerBase
{
    private readonly IWorkflowPrevisionUbService _service;

    public WorkflowPrevisionsUbController(IWorkflowPrevisionUbService service)
    {
        _service = service;
    }

    [HttpPost("soumettre")]
    public async Task<IActionResult> Soumettre(
        [FromBody] WorkflowUbActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SoumettreAsync(request.IdVersion, request.IdUB, cancellationToken);
        return Ok(result);
    }

    [HttpPost("controler")]
    public async Task<IActionResult> Controler(
        [FromBody] WorkflowUbActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ControlerAsync(request.IdVersion, request.IdUB, cancellationToken);
        return Ok(result);
    }

    [HttpPost("valider")]
    public async Task<IActionResult> Valider(
        [FromBody] WorkflowUbActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ValiderAsync(request.IdVersion, request.IdUB, cancellationToken);
        return Ok(result);
    }

    [HttpPost("rejeter")]
    public async Task<IActionResult> Rejeter(
        [FromBody] WorkflowUbRejetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.RejeterAsync(
            request.IdVersion, request.IdUB, request.Motif, cancellationToken);
        return Ok(result);
    }

    [HttpPost("soumettre-departement")]
    public async Task<IActionResult> SoumettreDepartement(
        [FromBody] WorkflowDepartementActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SoumettreDepartementAsync(
            request.IdVersion, request.IdDepartement, cancellationToken);
        return Ok(result);
    }

    [HttpPost("controler-departement")]
    public async Task<IActionResult> ControlerDepartement(
        [FromBody] WorkflowDepartementActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ControlerDepartementAsync(
            request.IdVersion, request.IdDepartement, cancellationToken);
        return Ok(result);
    }

    [HttpPost("valider-departement")]
    public async Task<IActionResult> ValiderDepartement(
        [FromBody] WorkflowDepartementActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ValiderDepartementAsync(
            request.IdVersion, request.IdDepartement, cancellationToken);
        return Ok(result);
    }

    [HttpPost("rejeter-departement")]
    public async Task<IActionResult> RejeterDepartement(
        [FromBody] WorkflowDepartementRejetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.RejeterDepartementAsync(
            request.IdVersion, request.IdDepartement, request.Motif, cancellationToken);
        return Ok(result);
    }

    [HttpPost("reouvrir")]
    public async Task<IActionResult> Reouvrir(
        [FromBody] WorkflowUbActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ReouvrirAsync(request.IdVersion, request.IdUB, cancellationToken);
        return Ok(result);
    }

    [HttpPost("annuler-soumission")]
    public async Task<IActionResult> AnnulerSoumission(
        [FromBody] WorkflowUbActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.AnnulerSoumissionAsync(request.IdVersion, request.IdUB, cancellationToken);
        return Ok(result);
    }
}
