using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IHistoriquePrevisionRepository
{
    Task<HistoriquePrevisionPageDto> QueryAsync(
        HistoriquePrevisionQuery query,
        long currentUserId,
        bool peutVoirToutes,
        CancellationToken cancellationToken = default);

    Task<HistoriquePrevisionTimelineDto?> GetTimelineAsync(
        long idVersion,
        long idUB,
        long currentUserId,
        bool peutVoirToutes,
        CancellationToken cancellationToken = default);
}

public interface IHistoriquePrevisionService
{
    Task<HistoriquePrevisionPageDto> QueryAsync(
        HistoriquePrevisionQuery query,
        CancellationToken cancellationToken = default);

    Task<HistoriquePrevisionTimelineDto?> GetTimelineAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default);
}
