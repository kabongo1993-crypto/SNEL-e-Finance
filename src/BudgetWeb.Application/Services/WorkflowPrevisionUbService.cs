using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public class WorkflowPrevisionUbService : IWorkflowPrevisionUbService
{
    private readonly IWorkflowPrevisionUbRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentPrevisionService _documents;
    private readonly IPerimetreAccesService _perimetreAcces;

    public WorkflowPrevisionUbService(
        IWorkflowPrevisionUbRepository repository,
        ICurrentUserService currentUser,
        IDocumentPrevisionService documents,
        IPerimetreAccesService perimetreAcces)
    {
        _repository = repository;
        _currentUser = currentUser;
        _documents = documents;
        _perimetreAcces = perimetreAcces;
    }

    public async Task EnsureExistsAsync(long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken = default)
        => await _repository.EnsureExistsAsync(idVersion, idUB, idUtilisateur, cancellationToken);

    public async Task<string> GetStatutOperationnelAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var statut = await _repository.GetStatutAsync(idVersion, idUB, cancellationToken);
        return string.IsNullOrWhiteSpace(statut)
            ? StatutVersionBudgetaire.Brouillon
            : StatutVersionBudgetaire.Normaliser(statut);
    }

    public async Task ReouvrirSiRejeteeApresSaisieAsync(
        long idVersion,
        long idUB,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasPermission(AppPermissions.PrevisionsEcrire)
            && !_currentUser.HasPermission(AppPermissions.AdminAll))
        {
            return;
        }

        await _repository.EnsureExistsAsync(idVersion, idUB, idUtilisateur, cancellationToken);
        var current = await _repository.GetAsync(idVersion, idUB, cancellationToken);
        if (current is null)
        {
            return;
        }

        if (!string.Equals(
                StatutVersionBudgetaire.Normaliser(current.Statut),
                StatutVersionBudgetaire.Rejetee,
                StringComparison.Ordinal))
        {
            return;
        }

        await _repository.TransitionnerUbAsync(
            idVersion,
            idUB,
            StatutVersionBudgetaire.Brouillon,
            idUtilisateur,
            "REOUVRIR_UB_APRES_SAISIE",
            "UB",
            _ => { },
            null,
            cancellationToken);
    }

    public Task RecalculerStatutVersionAsync(long idVersion, CancellationToken cancellationToken = default)
        => _repository.RecalculerStatutVersionAsync(idVersion, _currentUser.RequireUserId(), cancellationToken);

    public async Task<WorkflowPrevisionUbDto> SoumettreAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.PrevisionsSoumettre);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteAsync(idVersion, idUB, cancellationToken);
        await _repository.EnsureExistsAsync(idVersion, idUB, userId, cancellationToken);

        await _repository.TransitionnerUbAsync(
            idVersion,
            idUB,
            StatutVersionBudgetaire.Soumise,
            userId,
            "SOUMETTRE_UB",
            "UB",
            e =>
            {
                e.FK_UtilisateurSoumission = userId;
                e.DateSoumission = DateTime.Now;
            },
            null,
            cancellationToken);

        var doc = await TryDocumentAsync(async () =>
            (DocumentPrevisionDto?)await _documents.GenererSoumissionUbAsync(idVersion, idUB, userId, null, cancellationToken));
        var dto = await RequireGetAsync(idVersion, idUB, cancellationToken);
        return dto with { Document = doc };
    }

    public async Task<WorkflowPrevisionUbDto> ControlerAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsControler);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteAsync(idVersion, idUB, cancellationToken);

        await _repository.TransitionnerUbAsync(
            idVersion,
            idUB,
            StatutVersionBudgetaire.Controlee,
            userId,
            "CONTROLER_UB",
            "UB",
            e =>
            {
                e.FK_UtilisateurControle = userId;
                e.DateControle = DateTime.Now;
            },
            null,
            cancellationToken);

        var doc = await TryDocumentAsync(async () =>
            (DocumentPrevisionDto?)await _documents.GenererControleUbAsync(idVersion, idUB, userId, null, cancellationToken));
        var dto = await RequireGetAsync(idVersion, idUB, cancellationToken);
        return dto with { Document = doc };
    }

    public async Task<WorkflowPrevisionUbDto> ValiderAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsValider);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteAsync(idVersion, idUB, cancellationToken);

        await _repository.TransitionnerUbAsync(
            idVersion,
            idUB,
            StatutVersionBudgetaire.Validee,
            userId,
            "VALIDER_UB",
            "UB",
            e =>
            {
                e.FK_UtilisateurValidation = userId;
                e.DateValidation = DateTime.Now;
            },
            null,
            cancellationToken);

        var doc = await TryDocumentAsync(async () =>
            (DocumentPrevisionDto?)await _documents.GenererValidationUbAsync(idVersion, idUB, userId, null, cancellationToken));
        var dto = await RequireGetAsync(idVersion, idUB, cancellationToken);
        return dto with { Document = doc };
    }

    public async Task<WorkflowPrevisionUbDto> RejeterAsync(
        long idVersion,
        long idUB,
        string motif,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsRejeter);
        var userId = _currentUser.RequireUserId();
        var motifNorm = NormaliserMotif(motif);
        await ValiderContexteAsync(idVersion, idUB, cancellationToken);

        var avant = await _repository.GetStatutAsync(idVersion, idUB, cancellationToken);
        await _repository.TransitionnerUbAsync(
            idVersion,
            idUB,
            StatutVersionBudgetaire.Rejetee,
            userId,
            "REJET_UB",
            "UB",
            e =>
            {
                e.FK_UtilisateurRejet = userId;
                e.DateRejet = DateTime.Now;
                e.MotifRejet = motifNorm;
            },
            new { motif = motifNorm },
            cancellationToken);

        var doc = await TryDocumentAsync(async () =>
            (DocumentPrevisionDto?)await _documents.GenererRejetUbAsync(
                idVersion, idUB, userId, motifNorm, avant, null, cancellationToken));
        var dto = await RequireGetAsync(idVersion, idUB, cancellationToken);
        return dto with { Document = doc };
    }

    public async Task<WorkflowDepartementBulkResultDto> SoumettreDepartementAsync(
        long idVersion,
        long idDepartement,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.PrevisionsSoumettre);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteDepartementAsync(idVersion, idDepartement, cancellationToken);
        var result = await _repository.SoumettreDepartementAsync(idVersion, idDepartement, userId, cancellationToken);
        var ubs = UbTraitees(result);
        DocumentPrevisionDto? doc = null;
        if (ubs.Count > 0)
        {
            doc = await TryDocumentAsync(() =>
                _documents.GenererSoumissionDepartementAsync(idVersion, idDepartement, userId, null, ubs, cancellationToken));
        }

        return result with { Document = doc };
    }

    public async Task<WorkflowDepartementBulkResultDto> ControlerDepartementAsync(
        long idVersion,
        long idDepartement,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsControler);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteDepartementAsync(idVersion, idDepartement, cancellationToken);
        var result = await _repository.ControlerDepartementAsync(idVersion, idDepartement, userId, cancellationToken);
        var ubs = UbTraitees(result);
        DocumentPrevisionDto? doc = null;
        if (ubs.Count > 0)
        {
            doc = await TryDocumentAsync(() =>
                _documents.GenererControleDepartementAsync(idVersion, idDepartement, userId, null, ubs, cancellationToken));
        }

        return result with { Document = doc };
    }

    public async Task<WorkflowDepartementBulkResultDto> ValiderDepartementAsync(
        long idVersion,
        long idDepartement,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsValider);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteDepartementAsync(idVersion, idDepartement, cancellationToken);
        var result = await _repository.ValiderDepartementAsync(idVersion, idDepartement, userId, cancellationToken);
        var ubs = UbTraitees(result);
        DocumentPrevisionDto? doc = null;
        if (ubs.Count > 0)
        {
            doc = await TryDocumentAsync(() =>
                _documents.GenererValidationDepartementAsync(idVersion, idDepartement, userId, null, ubs, cancellationToken));
        }

        return result with { Document = doc };
    }

    public async Task<WorkflowDepartementBulkResultDto> RejeterDepartementAsync(
        long idVersion,
        long idDepartement,
        string motif,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.VersionsRejeter);
        var userId = _currentUser.RequireUserId();
        var motifNorm = NormaliserMotif(motif);
        await ValiderContexteDepartementAsync(idVersion, idDepartement, cancellationToken);
        var result = await _repository.RejeterDepartementAsync(
            idVersion, idDepartement, motifNorm, userId, cancellationToken);
        var ubs = result.Details
            .Where(d => !string.Equals(d.Outcome, "IGNORE", StringComparison.OrdinalIgnoreCase))
            .Select(d => (d.IdUB, (string?)d.StatutAvant))
            .ToList();
        DocumentPrevisionDto? doc = null;
        if (ubs.Count > 0)
        {
            doc = await TryDocumentAsync(() =>
                _documents.GenererRejetDepartementAsync(
                    idVersion, idDepartement, userId, motifNorm, null, ubs, cancellationToken));
        }

        return result with { Document = doc };
    }

    public async Task<WorkflowPrevisionUbDto> ReouvrirAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.PrevisionsEcrire);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteAsync(idVersion, idUB, cancellationToken);

        await _repository.TransitionnerUbAsync(
            idVersion,
            idUB,
            StatutVersionBudgetaire.Brouillon,
            userId,
            "REOUVRIR_UB",
            "UB",
            _ => { },
            null,
            cancellationToken);

        return await RequireGetAsync(idVersion, idUB, cancellationToken);
    }

    public async Task<WorkflowPrevisionUbDto> AnnulerSoumissionAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        ExigerPermission(AppPermissions.PrevisionsSoumettre);
        var userId = _currentUser.RequireUserId();
        await ValiderContexteAsync(idVersion, idUB, cancellationToken);

        await _repository.TransitionnerUbAsync(
            idVersion,
            idUB,
            StatutVersionBudgetaire.Brouillon,
            userId,
            "ANNULER_SOUMISSION_UB",
            "UB",
            e =>
            {
                e.FK_UtilisateurSoumission = null;
                e.DateSoumission = null;
            },
            null,
            cancellationToken);

        return await RequireGetAsync(idVersion, idUB, cancellationToken);
    }

    private static IReadOnlyList<long> UbTraitees(WorkflowDepartementBulkResultDto result)
        => result.Details
            .Where(d => !string.Equals(d.Outcome, "IGNORE", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.IdUB)
            .Distinct()
            .ToList();

    /// <summary>
    /// La génération documentaire ne doit pas annuler une transition déjà commitée.
    /// </summary>
    private static async Task<DocumentPrevisionDto?> TryDocumentAsync(Func<Task<DocumentPrevisionDto?>> action)
    {
        try
        {
            return await action();
        }
        catch
        {
            return null;
        }
    }

    private async Task ValiderContexteDepartementAsync(
        long idVersion,
        long idDepartement,
        CancellationToken cancellationToken)
    {
        if (!await _repository.ExistsVersionAsync(idVersion, cancellationToken))
        {
            throw new InvalidOperationException("La version budgétaire indiquée n'existe pas.");
        }

        if (idDepartement <= 0)
        {
            throw new InvalidOperationException("Le département est obligatoire.");
        }
    }

    private async Task ValiderContexteAsync(long idVersion, long idUB, CancellationToken cancellationToken)
    {
        if (!await _repository.ExistsVersionAsync(idVersion, cancellationToken))
        {
            throw new InvalidOperationException("La version budgétaire indiquée n'existe pas.");
        }

        if (!await _repository.ExistsUbAsync(idUB, cancellationToken))
        {
            throw new InvalidOperationException("L'unité budgétaire indiquée n'existe pas.");
        }

        if (!await _repository.ExistsPrevisionForPairAsync(idVersion, idUB, cancellationToken))
        {
            throw new InvalidOperationException(
                "Aucune prévision n'existe pour cette version et cette unité budgétaire.");
        }

        if (!_perimetreAcces.PeutVoirToutesUbPrevisions())
        {
            await _perimetreAcces.GarantirAccesUbSaisiePrevisionsAsync(idUB, cancellationToken);
        }
    }

    private async Task<WorkflowPrevisionUbDto> RequireGetAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken)
        => await _repository.GetAsync(idVersion, idUB, cancellationToken)
           ?? throw new InvalidOperationException("Workflow Version×UB introuvable après transition.");

    private static string NormaliserMotif(string motif)
    {
        var motifNorm = (motif ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(motifNorm))
        {
            throw new InvalidOperationException("Le motif du rejet est obligatoire.");
        }

        if (motifNorm.Length > 1000)
        {
            throw new InvalidOperationException("Le motif du rejet ne peut pas dépasser 1000 caractères.");
        }

        return motifNorm;
    }

    private void ExigerPermission(string permission)
    {
        if (!_currentUser.HasPermission(permission) && !_currentUser.HasPermission(AppPermissions.AdminAll))
        {
            throw new UnauthorizedAccessException($"Permission requise : {permission}.");
        }
    }
}
