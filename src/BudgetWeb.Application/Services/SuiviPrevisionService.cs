using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public class SuiviPrevisionService : ISuiviPrevisionService
{
    private readonly ISuiviPrevisionRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPerimetreAccesService _perimetreAcces;
    private readonly IPerimetreUtilisateurReader _perimetre;

    public SuiviPrevisionService(
        ISuiviPrevisionRepository repository,
        ICurrentUserService currentUser,
        IPerimetreAccesService perimetreAcces,
        IPerimetreUtilisateurReader perimetre)
    {
        _repository = repository;
        _currentUser = currentUser;
        _perimetreAcces = perimetreAcces;
        _perimetre = perimetre;
    }

    public async Task<SuiviPrevisionListeDto> GetMesPrevisionsAsync(
        long? idExercice,
        long? idVersion,
        long? idDepartement,
        string? statut,
        string? searchUb,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        var snapshot = await _perimetre.GetAsync(userId, cancellationToken);

        if (PerimetreAccess.EstConfigure(snapshot))
        {
            var all = await _repository.GetResumeParUbAsync(
                null, idExercice, idVersion, idDepartement, NormaliserStatut(statut), searchUb, cancellationToken);
            var lignes = new List<SuiviPrevisionUbResumeDto>();
            foreach (var l in all.Lignes)
            {
                if (await _perimetreAcces.PeutAccederUbLectureCourantAsync(l.IdUB, cancellationToken))
                    lignes.Add(l);
            }

            return new SuiviPrevisionListeDto(RecalculerCompteurs(lignes), lignes);
        }

        return await _repository.GetResumeParUbAsync(
            userId, idExercice, idVersion, idDepartement, NormaliserStatut(statut), searchUb, cancellationToken);
    }

    public async Task<SuiviPrevisionListeDto> GetSoumissionsAsync(
        long? idExercice,
        long? idVersion,
        long? idDepartement,
        string? statut,
        string? searchUb,
        CancellationToken cancellationToken = default)
    {
        ExigerAccesSoumissions();

        var statutNorm = NormaliserStatut(statut);
        if (string.IsNullOrEmpty(statutNorm))
        {
            var all = await _repository.GetResumeParUbAsync(
                null, idExercice, idVersion, idDepartement, null, searchUb, cancellationToken);
            var lignes = all.Lignes
                .Where(l => Norm(l.Statut) != StatutVersionBudgetaire.Brouillon)
                .ToList();
            return new SuiviPrevisionListeDto(RecalculerCompteurs(lignes), lignes);
        }

        return await _repository.GetResumeParUbAsync(
            null, idExercice, idVersion, idDepartement, statutNorm, searchUb, cancellationToken);
    }

    public async Task<SuiviUbDetailDto?> GetUbDetailAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var detail = await _repository.GetUbDetailAsync(idVersion, idUB, cancellationToken);
        if (detail is null) return null;

        if (!_perimetreAcces.PeutVoirToutesUbPrevisions())
        {
            if (!await _perimetreAcces.PeutAccederUbLectureCourantAsync(idUB, cancellationToken))
            {
                throw new UnauthorizedAccessException(
                    "Vous n'avez pas accès au détail de cette unité budgétaire.");
            }
        }

        var statut = Norm(detail.Statut);
        var peutControler = statut == StatutVersionBudgetaire.Soumise
            && (_currentUser.HasPermission(AppPermissions.VersionsControler)
                || _currentUser.HasPermission(AppPermissions.AdminAll));
        var peutValider = statut == StatutVersionBudgetaire.Controlee
            && (_currentUser.HasPermission(AppPermissions.VersionsValider)
                || _currentUser.HasPermission(AppPermissions.AdminAll));
        var peutRejeter = (statut == StatutVersionBudgetaire.Soumise || statut == StatutVersionBudgetaire.Controlee)
            && (_currentUser.HasPermission(AppPermissions.VersionsRejeter)
                || _currentUser.HasPermission(AppPermissions.AdminAll));

        return detail with
        {
            PeutControler = peutControler,
            PeutValider = peutValider,
            PeutRejeter = peutRejeter,
        };
    }

    public async Task<SuiviUbDetailLignesDto> GetUbLignesAsync(
        long idVersion,
        long idUB,
        string codeType,
        CancellationToken cancellationToken = default)
    {
        var header = await GetUbDetailAsync(idVersion, idUB, cancellationToken);
        if (header is null)
        {
            throw new KeyNotFoundException("Aucune prévision pour cette version et cette UB.");
        }

        return await _repository.GetUbLignesAsync(idVersion, idUB, codeType, cancellationToken);
    }

    private void ExigerAccesSoumissions()
    {
        if (!PeutVoirSoumissions())
        {
            throw new UnauthorizedAccessException(
                "Permission insuffisante pour consulter les soumissions budgétaires.");
        }
    }

    private bool PeutVoirSoumissions()
        => _perimetreAcces.PeutVoirToutesUbPrevisions();

    private static string? NormaliserStatut(string? statut)
    {
        if (string.IsNullOrWhiteSpace(statut)) return null;
        var s = statut.Trim().ToUpperInvariant();
        return StatutVersionBudgetaire.IsValid(s) ? s : null;
    }

    private static string Norm(string statut) => (statut ?? string.Empty).Trim().ToUpperInvariant();

    private static SuiviPrevisionCompteursDto RecalculerCompteurs(IReadOnlyList<SuiviPrevisionUbResumeDto> lignes)
        => new(
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Brouillon),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Soumise),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Controlee),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Validee),
            lignes.Count(l => Norm(l.Statut) == StatutVersionBudgetaire.Rejetee));
}
