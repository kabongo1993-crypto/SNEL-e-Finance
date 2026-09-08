using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IDemandePaiementBatchService
{
    Task<DemandePaiementBatchResultDto> ExecuterAsync(
        DemandePaiementBatchOperation operation,
        DemandePaiementBatchRequest request,
        CancellationToken cancellationToken = default);
}