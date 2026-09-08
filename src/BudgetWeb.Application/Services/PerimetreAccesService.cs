using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class PerimetreAccesService : IPerimetreAccesService
{
    private readonly IPerimetreUtilisateurReader _perimetre;
    private readonly ICurrentUserService _currentUser;

    public PerimetreAccesService(
        IPerimetreUtilisateurReader perimetre,
        ICurrentUserService currentUser)
    {
        _perimetre = perimetre;
        _currentUser = currentUser;
    }

    public bool PeutVoirToutesUbPrevisions()
        => _currentUser.HasPermission(AppPermissions.VersionsControler)
           || _currentUser.HasPermission(AppPermissions.VersionsValider)
           || _currentUser.HasPermission(AppPermissions.VersionsRejeter)
           || _currentUser.HasPermission(AppPermissions.AdminAll);

    public async Task GarantirAccesUbSaisiePrevisionsAsync(
        long idUB,
        CancellationToken cancellationToken = default)
    {
        if (PeutVoirToutesUbPrevisions())
            return;

        var userId = _currentUser.RequireUserId();
        var snapshot = await _perimetre.GetAsync(userId, cancellationToken);
        if (!PerimetreAccess.EstConfigure(snapshot))
            return;

        if (!await PeutAccederUbSnapshotAsync(snapshot!, idUB, cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Vous n'avez pas accès à cette unité budgétaire pour la saisie des prévisions.");
        }
    }

    public async Task GarantirAccesUbLecturePrevisionsAsync(
        long idUB,
        CancellationToken cancellationToken = default)
    {
        if (PeutVoirToutesUbPrevisions())
            return;

        var userId = _currentUser.RequireUserId();
        if (!await UtilisateurPeutAccederUbLecturePrevisionsAsync(userId, idUB, cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Vous n'avez pas accès aux prévisions de cette unité budgétaire.");
        }
    }

    public async Task<bool> UtilisateurPeutAccederUbLecturePrevisionsAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        if (PeutVoirToutesUbPrevisions())
            return true;

        var snapshot = await _perimetre.GetAsync(idUtilisateur, cancellationToken);
        if (PerimetreAccess.EstConfigure(snapshot))
            return await PeutAccederUbSnapshotAsync(snapshot!, idUB, cancellationToken);

        return await _perimetre.UtilisateurACreePrevisionSurUbAsync(
            idUtilisateur, idUB, cancellationToken);
    }

    public Task<bool> PeutAccederUbLectureCourantAsync(
        long idUB,
        CancellationToken cancellationToken = default)
        => UtilisateurPeutAccederUbLecturePrevisionsAsync(
            _currentUser.RequireUserId(), idUB, cancellationToken);

    public async Task<IReadOnlyList<long>?> GetIdsUbAutoriseesSaisieAsync(
        CancellationToken cancellationToken = default)
    {
        if (PeutVoirToutesUbPrevisions())
            return null;

        var userId = _currentUser.RequireUserId();
        var snapshot = await _perimetre.GetAsync(userId, cancellationToken);
        if (!PerimetreAccess.EstConfigure(snapshot))
            return null;

        var ids = await _perimetre.ResoudreIdsUbPerimetreAsync(userId, cancellationToken);
        return ids;
    }

    public bool PeutVoirToutesUbDpm()
        => _currentUser.HasPermission(AppPermissions.AdminAll)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerDc)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerAe)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerBi)
           || _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
           || _currentUser.HasPermission(AppPermissions.PaiementsViserBudget);

    public async Task GarantirAccesUbDpmAsync(
        long idUB,
        CancellationToken cancellationToken = default)
    {
        if (PeutVoirToutesUbDpm())
            return;

        if (!await PeutAccederUbDpmAsync(idUB, cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Vous n'avez pas accès à cette unité budgétaire.");
        }
    }

    public async Task<bool> PeutAccederUbDpmAsync(
        long idUB,
        CancellationToken cancellationToken = default)
    {
        if (PeutVoirToutesUbDpm())
            return true;

        var userId = _currentUser.RequireUserId();
        return await _perimetre.UtilisateurPeutAccederUbAsync(userId, idUB, cancellationToken);
    }

    public async Task<IReadOnlyList<long>?> GetIdsUbAutoriseesDpmAsync(
        CancellationToken cancellationToken = default)
    {
        if (PeutVoirToutesUbDpm())
            return null;

        var userId = _currentUser.RequireUserId();
        var snapshot = await _perimetre.GetAsync(userId, cancellationToken);
        if (PerimetreAccess.EstConfigure(snapshot))
            return await _perimetre.ResoudreIdsUbPerimetreAsync(userId, cancellationToken);

        return await _perimetre.ResoudreIdsUbProxyPrevisionAsync(userId, cancellationToken);
    }

    private async Task<bool> PeutAccederUbSnapshotAsync(
        PerimetreUtilisateurSnapshot snapshot,
        long idUB,
        CancellationToken cancellationToken)
    {
        var idDepartement = await _perimetre.GetDepartementUbAsync(idUB, cancellationToken);
        if (idDepartement is null)
            return false;

        return PerimetreAccess.PeutAccederUb(snapshot, idUB, idDepartement.Value);
    }
}
