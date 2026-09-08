using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public class HistoriquePrevisionService : IHistoriquePrevisionService
{
    private readonly IHistoriquePrevisionRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public HistoriquePrevisionService(
        IHistoriquePrevisionRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public Task<HistoriquePrevisionPageDto> QueryAsync(
        HistoriquePrevisionQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        var peutVoirToutes = PeutVoirToutes();
        var monHistorique = query.MonHistorique;
        if (!peutVoirToutes)
        {
            monHistorique = true;
        }

        var normalized = query with
        {
            MonHistorique = monHistorique,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize,
            Action = string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim().ToUpperInvariant(),
            Statut = string.IsNullOrWhiteSpace(query.Statut) ? null : query.Statut.Trim().ToUpperInvariant(),
            Type = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim().ToUpperInvariant(),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
        };

        return _repository.QueryAsync(normalized, userId, peutVoirToutes, cancellationToken);
    }

    public Task<HistoriquePrevisionTimelineDto?> GetTimelineAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        return _repository.GetTimelineAsync(idVersion, idUB, userId, PeutVoirToutes(), cancellationToken);
    }

    private bool PeutVoirToutes()
        => _currentUser.HasPermission(AppPermissions.VersionsControler)
           || _currentUser.HasPermission(AppPermissions.VersionsValider)
           || _currentUser.HasPermission(AppPermissions.VersionsRejeter)
           || _currentUser.HasPermission(AppPermissions.AdminAll);
}
