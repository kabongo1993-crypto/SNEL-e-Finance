using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class AjustementBudgetaireService : IAjustementBudgetaireService
{
    private readonly IAjustementBudgetaireRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public AjustementBudgetaireService(
        IAjustementBudgetaireRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AjustementBudgetaireDto>> GetAllAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(query, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<AjustementBudgetaireDto?> GetByIdAsync(
        long idAjustement, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idAjustement, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<AjustementHistoriqueLigneDto> GetHistoriqueLigneAsync(
        long idPrevision, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var prevision = await _repository.GetPrevisionAsync(idPrevision, cancellationToken)
            ?? throw new InvalidOperationException("Prévision introuvable.");

        var ajustements = await _repository.ListByPrevisionAsync(idPrevision, cancellationToken);
        var valides = ajustements
            .Where(a => a.Statut == StatutAjustementBudgetaire.Valide)
            .OrderBy(a => a.DateValidation ?? a.DateCreation)
            .ToList();

        var montantActuel = prevision.MontantAnnuel;
        var montantInitial = valides.Count == 0
            ? montantActuel
            : valides[0].MontantAncien;

        return new AjustementHistoriqueLigneDto(
            idPrevision,
            LibelleLigne(prevision),
            montantInitial,
            montantActuel,
            ajustements.Select(Map).ToList());
    }

    public async Task<IReadOnlyList<AjustementLigneCandidateDto>> GetLignesDisponiblesAsync(
        AjustementBudgetaireQuery query, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var exerciceCourant = await _repository.GetExerciceCourantAsync(cancellationToken);
        var effectiveQuery = query;
        // Écran centré sur l'exercice courant si aucun filtre exercice n'est fourni.
        if (query.IdExercice is null && exerciceCourant is not null)
        {
            effectiveQuery = query with { IdExercice = exerciceCourant.IdExercice };
        }

        var lignes = await _repository.GetPrevisionsValideesAsync(effectiveQuery, cancellationToken);
        return await MapLignesAsync(lignes, exerciceCourant?.IdExercice, cancellationToken);
    }

    private async Task<IReadOnlyList<AjustementLigneCandidateDto>> MapLignesAsync(
        IReadOnlyList<PrevisionBudgetaire> lignes,
        long? idExerciceCourant,
        CancellationToken cancellationToken)
    {
        var ids = lignes.Select(p => p.IdPrevision).ToList();
        var stats = await _repository.GetAjustementStatsByPrevisionAsync(ids, cancellationToken);

        return lignes.Select(p =>
        {
            stats.TryGetValue(p.IdPrevision, out var st);
            var montantActuel = p.MontantAnnuel;
            var montantInitial = st.FirstAncien ?? montantActuel;
            var dept = p.UniteBudgetaire.Departement;
            var estCourant = idExerciceCourant is long idEx && p.VersionBudgetaire.FK_ExerciceBudgetaire == idEx;
            return new AjustementLigneCandidateDto(
                p.IdPrevision,
                p.FK_VersionBudgetaire,
                p.VersionBudgetaire.NumeroVersion,
                p.VersionBudgetaire.Libelle,
                p.VersionBudgetaire.FK_ExerciceBudgetaire,
                p.VersionBudgetaire.ExerciceBudgetaire.Annee,
                dept.IdDepartement,
                dept.Code,
                dept.Libelle,
                p.FK_UniteBudgetaire,
                p.UniteBudgetaire.CodeUB,
                p.UniteBudgetaire.Libelle,
                p.TypeBudget.CodeType,
                LibelleLigne(p),
                montantInitial,
                montantActuel,
                p.ModePrevision.CodeMode,
                st.NbValides,
                st.HasBrouillon,
                estCourant);
        }).ToList();
    }

    public async Task<IReadOnlyList<AjustementLigneCandidateDto>> GetLignesValideesAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var statut = await _repository.GetWorkflowStatutAsync(idVersion, idUB, cancellationToken);
        if (!string.Equals(statut, StatutVersionBudgetaire.Validee, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ajustement impossible : le budget de cette UB n'est pas validé.");
        }

        var exerciceCourant = await _repository.GetExerciceCourantAsync(cancellationToken);
        var lignes = await _repository.GetPrevisionsValideesUbAsync(idVersion, idUB, cancellationToken);
        return await MapLignesAsync(lignes, exerciceCourant?.IdExercice, cancellationToken);
    }

    public async Task<AjustementBudgetaireDto> CreateAsync(
        CreateAjustementBudgetaireRequest request, CancellationToken cancellationToken = default)
    {
        ExigerEcrire();
        var userId = _currentUser.RequireUserId();
        if (request.MontantNouveau < 0)
            throw new ArgumentException("Le nouveau montant ne peut pas être négatif.");
        if (string.IsNullOrWhiteSpace(request.Motif))
            throw new ArgumentException("Le motif de l'ajustement est obligatoire.");

        var prevision = await _repository.GetPrevisionAsync(request.IdPrevision, cancellationToken)
            ?? throw new InvalidOperationException("Prévision introuvable.");

        await EnsureUbValideeAsync(prevision.FK_VersionBudgetaire, prevision.FK_UniteBudgetaire, cancellationToken);
        await EnsureExerciceCourantAsync(prevision, cancellationToken);

        if (await _repository.ExistsBrouillonPourPrevisionAsync(prevision.IdPrevision, null, cancellationToken))
        {
            throw new InvalidOperationException(
                "Un ajustement en brouillon existe déjà pour cette ligne budgétaire.");
        }

        if (request.MontantNouveau == prevision.MontantAnnuel)
            throw new ArgumentException("Le nouveau montant doit différer du montant actuel.");

        var count = await _repository.CountAsync(cancellationToken);
        var annee = prevision.VersionBudgetaire.ExerciceBudgetaire.Annee;
        var reference = $"AJT-{annee}-{(count + 1):D5}";

        var entity = new AjustementBudgetaire
        {
            Reference = reference,
            FK_PrevisionBudgetaire = prevision.IdPrevision,
            FK_VersionBudgetaire = prevision.FK_VersionBudgetaire,
            FK_UniteBudgetaire = prevision.FK_UniteBudgetaire,
            FK_ExerciceBudgetaire = prevision.VersionBudgetaire.FK_ExerciceBudgetaire,
            MontantAncien = prevision.MontantAnnuel,
            MontantNouveau = request.MontantNouveau,
            Variation = request.MontantNouveau - prevision.MontantAnnuel,
            Motif = request.Motif.Trim(),
            Statut = StatutAjustementBudgetaire.Brouillon,
            FK_UtilisateurCreation = userId,
            DateCreation = DateTime.Now,
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        await _repository.AddAuditAsync(userId, "CREER", created.IdAjustement, null, new
        {
            created.Reference,
            created.MontantAncien,
            created.MontantNouveau,
            created.Variation,
            created.Motif,
            created.Statut,
        }, cancellationToken);

        return Map(created);
    }

    public async Task<AjustementBudgetaireDto> UpdateAsync(
        long idAjustement, UpdateAjustementBudgetaireRequest request, CancellationToken cancellationToken = default)
    {
        ExigerEcrire();
        var userId = _currentUser.RequireUserId();
        if (request.MontantNouveau < 0)
            throw new ArgumentException("Le nouveau montant ne peut pas être négatif.");
        if (string.IsNullOrWhiteSpace(request.Motif))
            throw new ArgumentException("Le motif de l'ajustement est obligatoire.");

        var entity = await _repository.GetByIdAsync(idAjustement, cancellationToken)
            ?? throw new InvalidOperationException("Ajustement introuvable.");

        if (entity.Statut != StatutAjustementBudgetaire.Brouillon)
            throw new InvalidOperationException("Seul un ajustement en brouillon peut être modifié.");

        var prevision = await _repository.GetPrevisionAsync(entity.FK_PrevisionBudgetaire, cancellationToken)
            ?? throw new InvalidOperationException("Prévision introuvable.");

        await EnsureExerciceCourantAsync(prevision, cancellationToken);

        var tracked = await _repository.GetTrackedAsync(idAjustement, cancellationToken)
            ?? throw new InvalidOperationException("Ajustement introuvable.");
        var ancienSnapshot = new { tracked.MontantNouveau, tracked.Motif, tracked.MontantAncien };
        tracked.MontantAncien = prevision.MontantAnnuel;
        tracked.MontantNouveau = request.MontantNouveau;
        tracked.Variation = request.MontantNouveau - prevision.MontantAnnuel;
        tracked.Motif = request.Motif.Trim();
        tracked.FK_UtilisateurModification = userId;
        tracked.DateModification = DateTime.Now;

        if (tracked.Variation == 0)
            throw new ArgumentException("Le nouveau montant doit différer du montant actuel.");

        await _repository.SaveAsync(cancellationToken);
        await _repository.AddAuditAsync(userId, "MODIFIER", idAjustement, ancienSnapshot, new
        {
            tracked.MontantAncien,
            tracked.MontantNouveau,
            tracked.Variation,
            tracked.Motif,
        }, cancellationToken);

        return (await GetByIdAsync(idAjustement, cancellationToken))!;
    }

    public async Task<AjustementBudgetaireDto> ValiderAsync(
        long idAjustement, CancellationToken cancellationToken = default)
    {
        ExigerValider();
        var userId = _currentUser.RequireUserId();
        var entity = await _repository.GetTrackedAsync(idAjustement, cancellationToken)
            ?? throw new InvalidOperationException("Ajustement introuvable.");

        if (entity.Statut != StatutAjustementBudgetaire.Brouillon)
            throw new InvalidOperationException("Seul un ajustement en brouillon peut être validé.");

        var prevision = await _repository.GetPrevisionAsync(entity.FK_PrevisionBudgetaire, cancellationToken)
            ?? throw new InvalidOperationException("Prévision introuvable.");

        await EnsureUbValideeAsync(prevision.FK_VersionBudgetaire, prevision.FK_UniteBudgetaire, cancellationToken);
        await EnsureExerciceCourantAsync(prevision, cancellationToken);

        if (prevision.MontantAnnuel != entity.MontantAncien)
        {
            throw new InvalidOperationException(
                "Le montant de la ligne a changé depuis la création de l'ajustement. Recalculez le brouillon.");
        }

        // Appliquer le montant courant (rapports consomment MontantAnnuel).
        await _repository.ApplyMontantPrevisionAsync(
            entity.FK_PrevisionBudgetaire, entity.MontantNouveau, userId, cancellationToken);

        entity.Statut = StatutAjustementBudgetaire.Valide;
        entity.FK_UtilisateurValidation = userId;
        entity.DateValidation = DateTime.Now;
        await _repository.SaveAsync(cancellationToken);
        await _repository.AddAuditAsync(userId, "VALIDER", idAjustement,
            new { statut = StatutAjustementBudgetaire.Brouillon, montant = entity.MontantAncien },
            new { statut = StatutAjustementBudgetaire.Valide, montant = entity.MontantNouveau },
            cancellationToken);

        return (await GetByIdAsync(idAjustement, cancellationToken))!;
    }

    public async Task<AjustementBudgetaireDto> AnnulerAsync(
        long idAjustement, CancellationToken cancellationToken = default)
    {
        ExigerEcrire();
        var userId = _currentUser.RequireUserId();
        var entity = await _repository.GetTrackedAsync(idAjustement, cancellationToken)
            ?? throw new InvalidOperationException("Ajustement introuvable.");

        if (entity.Statut != StatutAjustementBudgetaire.Brouillon)
            throw new InvalidOperationException("Seul un ajustement en brouillon peut être annulé.");

        entity.Statut = StatutAjustementBudgetaire.Annule;
        entity.FK_UtilisateurModification = userId;
        entity.DateModification = DateTime.Now;
        await _repository.SaveAsync(cancellationToken);
        await _repository.AddAuditAsync(userId, "ANNULER", idAjustement,
            new { statut = StatutAjustementBudgetaire.Brouillon },
            new { statut = StatutAjustementBudgetaire.Annule },
            cancellationToken);

        return (await GetByIdAsync(idAjustement, cancellationToken))!;
    }

    private async Task EnsureUbValideeAsync(long idVersion, long idUB, CancellationToken cancellationToken)
    {
        var statut = await _repository.GetWorkflowStatutAsync(idVersion, idUB, cancellationToken);
        if (!string.Equals(statut, StatutVersionBudgetaire.Validee, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ajustement interdit : le budget de l'UB n'est pas validé.");
        }
    }

    private async Task EnsureExerciceCourantAsync(PrevisionBudgetaire prevision, CancellationToken cancellationToken)
    {
        var courant = await _repository.GetExerciceCourantAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Aucun exercice budgétaire ouvert : ajustement impossible.");

        if (prevision.VersionBudgetaire.FK_ExerciceBudgetaire != courant.IdExercice)
        {
            throw new InvalidOperationException(
                $"Ajustement autorisé uniquement sur l'exercice courant ({courant.Annee}). " +
                $"La ligne appartient à l'exercice {prevision.VersionBudgetaire.ExerciceBudgetaire.Annee}.");
        }

        if (!string.Equals(
                prevision.VersionBudgetaire.ExerciceBudgetaire.Statut,
                StatutExerciceBudgetaire.Ouvert,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ajustement interdit : l'exercice de cette ligne n'est pas ouvert.");
        }
    }

    private void ExigerLecture()
    {
        if (_currentUser.HasPermission(AppPermissions.AjustementsLire)
            || _currentUser.HasPermission(AppPermissions.AjustementsEcrire)
            || _currentUser.HasPermission(AppPermissions.AjustementsValider)
            || _currentUser.HasPermission(AppPermissions.VersionsValider)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;
        throw new UnauthorizedAccessException("Permission requise : ajustements.lire.");
    }

    private void ExigerEcrire()
    {
        if (_currentUser.HasPermission(AppPermissions.AjustementsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;
        throw new UnauthorizedAccessException("Permission requise : ajustements.ecrire (DG).");
    }

    private void ExigerValider()
    {
        if (_currentUser.HasPermission(AppPermissions.AjustementsValider)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;
        throw new UnauthorizedAccessException("Permission requise : ajustements.valider (DG).");
    }

    private static string LibelleLigne(PrevisionBudgetaire p)
    {
        var type = p.TypeBudget.CodeType;
        return type.ToUpperInvariant() switch
        {
            "DC" => $"{p.RubriqueBudgetaire?.CodeRB} — {p.RubriqueBudgetaire?.Libelle}",
            "AE" => $"{p.RubriqueBudgetaire?.CodeRB} / {p.LibelleItemAE}",
            "BI" => $"{p.ItemBI?.CodeItem} — {p.DetailBI}",
            _ => $"Prévision #{p.IdPrevision}",
        };
    }

    private static string Nom(Utilisateur? u)
    {
        if (u is null) return "—";
        var parts = new[] { u.Prenom, u.Nom, u.Postnom }.Where(s => !string.IsNullOrWhiteSpace(s));
        var full = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(full) ? u.NomUtilisateur : full;
    }

    private static AjustementBudgetaireDto Map(AjustementBudgetaire a)
    {
        var p = a.PrevisionBudgetaire;
        return new AjustementBudgetaireDto(
            a.IdAjustement,
            a.Reference,
            a.FK_PrevisionBudgetaire,
            a.FK_VersionBudgetaire,
            a.VersionBudgetaire.NumeroVersion,
            a.VersionBudgetaire.Libelle,
            a.FK_ExerciceBudgetaire,
            a.VersionBudgetaire.ExerciceBudgetaire.Annee,
            a.FK_UniteBudgetaire,
            a.UniteBudgetaire.CodeUB,
            a.UniteBudgetaire.Libelle,
            p.TypeBudget.CodeType,
            LibelleLigne(p),
            a.MontantAncien,
            a.MontantNouveau,
            a.Variation,
            a.Motif,
            a.Statut,
            a.FK_UtilisateurCreation,
            Nom(a.UtilisateurCreation),
            a.DateCreation,
            a.FK_UtilisateurValidation,
            Nom(a.UtilisateurValidation),
            a.DateValidation);
    }
}
