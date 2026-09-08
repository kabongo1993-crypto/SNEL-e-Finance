using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public class VersionBudgetaireService : IVersionBudgetaireService
{
    private readonly IVersionBudgetaireRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public VersionBudgetaireService(
        IVersionBudgetaireRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<VersionBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<VersionBudgetaireDto?> GetByIdAsync(long idVersion, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idVersion, cancellationToken);

    public Task<IReadOnlyList<UtilisateurLookupDto>> GetUtilisateursAsync(CancellationToken cancellationToken = default)
        => _repository.GetUtilisateursAsync(cancellationToken);

    public async Task<VersionBudgetaireDto> CreateAsync(
        CreateVersionBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsEcrire);
        var champs = await ValiderMetadonneesAsync(
            request.IdExercice,
            request.NumeroVersion,
            request.Libelle,
            request.IdVersionPrecedente,
            request.DateDebutEffet,
            request.DateFinEffet,
            request.Motif,
            request.IdUtilisateurCreation,
            excludeId: null,
            cancellationToken);

        // Création : toujours BROUILLON (statut client ignoré).
        return await _repository.CreateAsync(
            champs.IdExercice,
            champs.NumeroVersion,
            champs.Libelle,
            champs.IdVersionPrecedente,
            champs.DateDebutEffet,
            champs.DateFinEffet,
            champs.Motif,
            StatutVersionBudgetaire.Brouillon,
            champs.IdUtilisateurCreation,
            cancellationToken);
    }

    public async Task<VersionBudgetaireDto?> UpdateAsync(
        long idVersion,
        UpdateVersionBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsEcrire);
        var existing = await _repository.GetByIdAsync(idVersion, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var champs = await ValiderMetadonneesAsync(
            request.IdExercice,
            request.NumeroVersion,
            request.Libelle,
            request.IdVersionPrecedente,
            request.DateDebutEffet,
            request.DateFinEffet,
            request.Motif,
            request.IdUtilisateurCreation,
            excludeId: idVersion,
            cancellationToken);

        if (champs.IdVersionPrecedente == idVersion)
        {
            throw new InvalidOperationException("Une version ne peut pas être sa propre version précédente.");
        }

        if (champs.IdVersionPrecedente is not null
            && await _repository.WouldCreateCycleAsync(idVersion, champs.IdVersionPrecedente.Value, cancellationToken))
        {
            throw new InvalidOperationException(
                "La version précédente indiquée créerait une boucle entre versions.");
        }

        return await _repository.UpdateAsync(
            idVersion,
            champs.IdExercice,
            champs.NumeroVersion,
            champs.Libelle,
            champs.IdVersionPrecedente,
            champs.DateDebutEffet,
            champs.DateFinEffet,
            champs.Motif,
            champs.IdUtilisateurCreation,
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idVersion, CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsEcrire);
        var existing = await _repository.GetByIdAsync(idVersion, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var suivantes = await _repository.CountVersionsSuivantesAsync(idVersion, cancellationToken);
        var previsions = await _repository.CountPrevisionsAsync(idVersion, cancellationToken);
        var transferts = await _repository.CountTransfertsAsync(idVersion, cancellationToken);

        if (suivantes > 0)
        {
            throw new InvalidOperationException(
                "Cette version ne peut pas être supprimée car elle est utilisée comme version précédente d'une ou plusieurs autres versions.");
        }

        if (previsions > 0)
        {
            throw new InvalidOperationException(
                "Cette version ne peut pas être supprimée car elle est utilisée par une ou plusieurs prévisions budgétaires.");
        }

        if (transferts > 0)
        {
            throw new InvalidOperationException(
                "Cette version ne peut pas être supprimée car elle est utilisée par un ou plusieurs transferts budgétaires.");
        }

        return await _repository.DeleteAsync(idVersion, cancellationToken);
    }

    public async Task<VersionBudgetaireDto> SoumettreAsync(
        long idVersion,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.PrevisionsSoumettre);
        return await TransitionnerAsync(
            idVersion,
            idUtilisateur,
            StatutVersionBudgetaire.Soumise,
            "SOUMETTRE",
            e =>
            {
                e.FK_UtilisateurSoumission = idUtilisateur;
                e.DateSoumission = DateTime.Now;
            },
            cancellationToken);
    }

    public async Task<VersionBudgetaireDto> ControlerAsync(
        long idVersion,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsControler);
        return await TransitionnerAsync(
            idVersion,
            idUtilisateur,
            StatutVersionBudgetaire.Controlee,
            "CONTROLER",
            e =>
            {
                e.FK_UtilisateurControle = idUtilisateur;
                e.DateControle = DateTime.Now;
            },
            cancellationToken);
    }

    public async Task<VersionBudgetaireDto> ValiderAsync(
        long idVersion,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsValider);
        return await TransitionnerAsync(
            idVersion,
            idUtilisateur,
            StatutVersionBudgetaire.Validee,
            "VALIDER",
            e =>
            {
                e.FK_UtilisateurValidation = idUtilisateur;
                e.DateValidation = DateTime.Now;
            },
            cancellationToken);
    }

    public async Task<VersionBudgetaireDto> RejeterAsync(
        long idVersion,
        long idUtilisateur,
        string motif,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsRejeter);
        var motifNorm = (motif ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(motifNorm))
        {
            throw new InvalidOperationException("Le motif du rejet est obligatoire.");
        }

        if (motifNorm.Length > 1000)
        {
            throw new InvalidOperationException("Le motif du rejet ne peut pas dépasser 1000 caractères.");
        }

        return await TransitionnerAsync(
            idVersion,
            idUtilisateur,
            StatutVersionBudgetaire.Rejetee,
            "REJETER",
            e =>
            {
                e.FK_UtilisateurRejet = idUtilisateur;
                e.DateRejet = DateTime.Now;
                e.MotifRejet = motifNorm;
            },
            cancellationToken);
    }

    public async Task<VersionBudgetaireDto> ReouvrirAsync(
        long idVersion,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.PrevisionsEcrire);
        return await TransitionnerAsync(
            idVersion,
            idUtilisateur,
            StatutVersionBudgetaire.Brouillon,
            "REOUVRIR",
            _ => { },
            cancellationToken);
    }

    public async Task ReouvrirSiRejeteeApresSaisieAsync(
        long idVersion,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idVersion, cancellationToken);
        if (existing is null)
        {
            return;
        }

        if (!string.Equals(
                StatutVersionBudgetaire.Normaliser(existing.Statut),
                StatutVersionBudgetaire.Rejetee,
                StringComparison.Ordinal))
        {
            return;
        }

        // Après saisie sur REJETEE : repasse en BROUILLON (permission déjà validée via prévisions.ecrire).
        if (!_currentUser.HasPermission(AppPermissions.PrevisionsEcrire)
            && !_currentUser.HasPermission(AppPermissions.AdminAll))
        {
            return;
        }

        await TransitionnerAsync(
            idVersion,
            idUtilisateur,
            StatutVersionBudgetaire.Brouillon,
            "REOUVRIR_APRES_SAISIE",
            _ => { },
            cancellationToken);
    }

    private async Task<VersionBudgetaireDto> TransitionnerAsync(
        long idVersion,
        long idUtilisateur,
        string vers,
        string operation,
        Action<Domain.Entities.VersionBudgetaire> appliquerTrace,
        CancellationToken cancellationToken)
    {
        if (idUtilisateur <= 0 || !await _repository.ExistsUtilisateurAsync(idUtilisateur, cancellationToken))
        {
            throw new InvalidOperationException("L'utilisateur authentifié est invalide.");
        }

        var existing = await _repository.GetByIdAsync(idVersion, cancellationToken)
            ?? throw new InvalidOperationException("La version budgétaire indiquée n'existe pas.");

        var de = existing.Statut;
        if (!StatutVersionBudgetaire.EstTransitionAutorisee(de, vers))
        {
            throw new InvalidOperationException(
                $"Transition interdite : {StatutVersionBudgetaire.Normaliser(de)} → {StatutVersionBudgetaire.Normaliser(vers)}.");
        }

        var updated = await _repository.AppliquerTransitionAsync(
            idVersion,
            StatutVersionBudgetaire.Normaliser(vers),
            idUtilisateur,
            operation,
            appliquerTrace,
            cancellationToken);

        return updated ?? throw new InvalidOperationException("La version budgétaire indiquée n'existe pas.");
    }

    private void ExigerPermission(string permission)
    {
        if (_currentUser.HasPermission(permission) || _currentUser.HasPermission(AppPermissions.AdminAll))
        {
            return;
        }

        throw new UnauthorizedAccessException($"Permission requise : {permission}.");
    }

    private async Task<ChampsValides> ValiderMetadonneesAsync(
        long idExercice,
        int? numeroVersion,
        string? libelleBrut,
        long? idVersionPrecedenteBrut,
        DateOnly? dateDebutEffet,
        DateOnly? dateFinEffet,
        string? motifBrut,
        long idUtilisateurCreation,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var libelle = (libelleBrut ?? string.Empty).Trim();
        var motif = string.IsNullOrWhiteSpace(motifBrut) ? null : motifBrut.Trim();
        var parentId = idVersionPrecedenteBrut is > 0 ? idVersionPrecedenteBrut : null;

        if (idExercice <= 0)
        {
            throw new InvalidOperationException("L'exercice budgétaire est obligatoire.");
        }

        if (!await _repository.ExistsExerciceAsync(idExercice, cancellationToken))
        {
            throw new InvalidOperationException("L'exercice budgétaire indiqué n'existe pas.");
        }

        if (numeroVersion is null)
        {
            throw new InvalidOperationException("Le numéro de version est obligatoire.");
        }

        if (numeroVersion.Value <= 0)
        {
            throw new InvalidOperationException("Le numéro de version doit être supérieur à 0.");
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé de la version est obligatoire.");
        }

        if (libelle.Length > 200)
        {
            throw new InvalidOperationException("Le libellé de la version ne peut pas dépasser 200 caractères.");
        }

        if (motif is { Length: > 1000 })
        {
            throw new InvalidOperationException("Le motif ne peut pas dépasser 1000 caractères.");
        }

        if (dateDebutEffet is null)
        {
            throw new InvalidOperationException("La date de début d'effet est obligatoire.");
        }

        if (dateFinEffet is not null && dateFinEffet.Value < dateDebutEffet.Value)
        {
            throw new InvalidOperationException(
                "La date de fin d'effet ne peut pas être antérieure à la date de début d'effet.");
        }

        if (parentId is not null)
        {
            if (!await _repository.ExistsVersionAsync(parentId.Value, cancellationToken))
            {
                throw new InvalidOperationException("La version précédente indiquée n'existe pas.");
            }
        }

        if (idUtilisateurCreation <= 0)
        {
            throw new InvalidOperationException("L'utilisateur de création est obligatoire.");
        }

        if (!await _repository.ExistsUtilisateurAsync(idUtilisateurCreation, cancellationToken))
        {
            throw new InvalidOperationException(
                "L'utilisateur de création indiqué n'existe pas. Un utilisateur doit exister dans le référentiel UTILISATEUR.");
        }

        if (await _repository.ExistsByExerciceNumeroAsync(idExercice, numeroVersion.Value, excludeId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"La version n° {numeroVersion.Value} existe déjà pour cet exercice.");
        }

        return new ChampsValides(
            idExercice,
            numeroVersion.Value,
            libelle,
            parentId,
            dateDebutEffet.Value,
            dateFinEffet,
            motif,
            idUtilisateurCreation);
    }

    private sealed record ChampsValides(
        long IdExercice,
        int NumeroVersion,
        string Libelle,
        long? IdVersionPrecedente,
        DateOnly DateDebutEffet,
        DateOnly? DateFinEffet,
        string? Motif,
        long IdUtilisateurCreation);
}
