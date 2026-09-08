using BudgetWeb.API.Models;
using BudgetWeb.Application.DpmBatch;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/demandes-paiement/batch")]
public class DemandePaiementsBatchController : ControllerBase
{
    private readonly IDemandePaiementBatchService _batchService;

    public DemandePaiementsBatchController(IDemandePaiementBatchService batchService)
    {
        _batchService = batchService;
    }

    [HttpPost("envoyer-validation")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> EnvoyerValidation(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.EnvoyerValidation, request, cancellationToken);

    [HttpPost("valider-n1")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> ValiderN1(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.ValiderN1, request, cancellationToken);

    [HttpPost("valider-n2")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> ValiderN2(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.ValiderN2, request, cancellationToken);

    [HttpPost("soumettre-budget")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> SoumettreBudget(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.SoumettreBudget, request, cancellationToken);

    [HttpPost("declarer-validation-physique-n1")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> DeclarerValidationPhysiqueN1(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1, request, cancellationToken);

    [HttpPost("declarer-validation-physique-n2")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> DeclarerValidationPhysiqueN2(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2, request, cancellationToken);

    [HttpPost("receptionner")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Receptionner(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.Receptionner, request, cancellationToken);

    [HttpPost("traiter-charge")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> TraiterCharge(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.TraiterCharge, request, cancellationToken);

    [HttpPost("etablir-documents")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> EtablirDocuments(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.EtablirDocuments, request, cancellationToken);

    [HttpPost("orienter")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Orienter(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.Orienter, request, cancellationToken);

    [HttpPost("supprimer")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Supprimer(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.SupprimerBrouillon, request, cancellationToken);

    [HttpPost("rejeter-validation-n1")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> RejeterValidationN1(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.RejeterValidationN1, request, cancellationToken);

    [HttpPost("rejeter-validation-n2")]
    [ProducesResponseType(typeof(DemandePaiementBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> RejeterValidationN2(
        [FromBody] DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(DemandePaiementBatchOperation.RejeterValidationN2, request, cancellationToken);

    private async Task<IActionResult> ExecuteAsync(
        DemandePaiementBatchOperation operation,
        DemandePaiementBatchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            DemandePaiementBatchOperationRules.ValidateRequest(operation, request);
        }
        catch (DemandePaiementBatchRequestException ex)
        {
            return BadRequest(new ApiErrorResponse
            {
                Status = StatusCodes.Status400BadRequest,
                Code = ex.ErrorCode,
                Message = ex.Message,
            });
        }

        var result = await _batchService.ExecuterAsync(operation, request, cancellationToken);
        return Ok(result);
    }
}