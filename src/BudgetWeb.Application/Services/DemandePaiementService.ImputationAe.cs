using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public async Task<GrilleImputationAeDto> GetGrilleImputationAeAsync(
        long idDemande,
        string libelleItemAE,
        long? idGroupeItemAE = null,
        byte? mois = null,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);
        ExigerTypeBudgetAe(demande);
        ExigerItemAe(libelleItemAE);
        ExigerMoisVentilationAe(mois);

        var typeAe = await _repository.GetTypeBudgetByCodeAsync(TypeBudgetCode.ActionsExploitation, cancellationToken)
            ?? throw new InvalidOperationException("Type budget AE introuvable.");

        return await ConstruireGrilleAeAsync(
            demande,
            typeAe,
            libelleItemAE.Trim(),
            idGroupeItemAE,
            mois,
            cancellationToken);
    }

    public async Task<GrilleImputationAeDto> EnregistrerImputationsAeAsync(
        long idDemande,
        EnregistrerImputationsAeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.RequireUserId();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        ExigerStatut(
            demande,
            StatutDemandePaiement.EnControleBudgetaire,
            "L'imputation est réservée au contrôle budgétaire (Gestionnaire Junior).");
        await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Imputer, cancellationToken);

        var typeAe = await _repository.GetTypeBudgetByCodeAsync(TypeBudgetCode.ActionsExploitation, cancellationToken)
            ?? throw new InvalidOperationException("Type budget AE introuvable.");

        ExigerImputer(typeAe.CodeType);
        ExigerTypeBudgetDemande(demande, typeAe.IdTypeBudget, typeAe.CodeType);
        ExigerItemAe(request.LibelleItemAE);
        ExigerMoisVentilationAe(request.Mois);

        var item = request.LibelleItemAE.Trim();
        var lignes = request.Lignes ?? [];
        var ids = new HashSet<long>();
        foreach (var ligne in lignes)
        {
            if (ligne.IdRubriqueBudgetaire <= 0)
                throw new ArgumentException("La rubrique budgétaire est obligatoire.");
            if (ligne.MontantBrut < 0m)
                throw new ArgumentException("Le montant d'imputation ne peut pas être négatif.");
            if (!ids.Add(ligne.IdRubriqueBudgetaire))
                throw new ArgumentException("Une même rubrique ne peut être imputée qu'une fois pour l'item et le mois.");
        }

        var (taux, _) = await ResoudreTauxImputationDcAsync(demande, cancellationToken);
        var devise = DemandePaiementMontants.NormaliserCodeDevise(demande.Devise);
        var idVersion = demande.FK_VersionBudgetaire
            ?? await _repository.ResolveVersionBudgetaireValideeAsync(
                demande.FK_ExerciceBudgetaire,
                demande.FK_UniteBudgetaire,
                cancellationToken);

        var aConserver = lignes.Where(l => l.MontantBrut > 0m).ToList();
        var maxOrdreAutres = demande.Imputations
            .Where(i => !(EstImputationAe(i)
                          && string.Equals((i.LibelleItemAE ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase)
                          && i.Mois == request.Mois))
            .Select(i => i.Ordre)
            .DefaultIfEmpty(0)
            .Max();

        var nouvelles = new List<DemandePaiementImputation>(aConserver.Count);
        var ordre = maxOrdreAutres;
        foreach (var ligne in aConserver.OrderBy(l => l.IdRubriqueBudgetaire))
        {
            ordre++;
            var montantUsd = DemandePaiementMontants.CalculerMontantUsd(ligne.MontantBrut, taux);
            var imputation = new DemandePaiementImputation
            {
                FK_DemandePaiement = idDemande,
                Ordre = ordre,
                FK_TypeBudget = typeAe.IdTypeBudget,
                FK_UniteBudgetaire = demande.FK_UniteBudgetaire,
                FK_ExerciceBudgetaire = demande.FK_ExerciceBudgetaire,
                FK_RubriqueBudgetaire = ligne.IdRubriqueBudgetaire,
                LibelleItemAE = item,
                FK_GroupeItemAE = request.IdGroupeItemAE,
                Mois = request.Mois,
                MontantBrut = ligne.MontantBrut,
                Devise = devise,
                TauxConversion = taux,
                MontantUsd = montantUsd,
                FK_UtilisateurCreation = userId,
                DateImputation = DateTime.Now,
            };

            if (idVersion is long versionId)
            {
                imputation.FK_BudgetLigne = await _repository.FindPrevisionIdAsync(
                    imputation,
                    versionId,
                    cancellationToken);
            }

            DemandePaiementImputationRules.ValiderStructure(imputation);
            DemandePaiementImputationRules.ValiderCoherenceTypeBudget(typeAe.CodeType, imputation);
            nouvelles.Add(imputation);
        }

        await AppliquerMiseAJourSiStatutAsync(
            idDemande,
            StatutDemandePaiement.EnControleBudgetaire,
            new DemandePaiementConditionalUpdatePatch
            {
                FK_UtilisateurModification = userId,
                DateModification = DateTime.Now,
            },
            cancellationToken);

        await _repository.ReplaceImputationsAeForItemMoisAsync(
            idDemande,
            item,
            request.Mois,
            nouvelles,
            cancellationToken);

        await _repository.AddAuditAsync(
            userId,
            "IMPUTER",
            idDemande,
            null,
            new
            {
                filiere = "AE",
                libelleItemAE = item,
                idGroupeItemAE = request.IdGroupeItemAE,
                mois = request.Mois,
                nbLignes = nouvelles.Count,
                totalUsd = nouvelles.Sum(i => i.MontantUsd),
            },
            cancellationToken);

        var rechargee = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");
        return await ConstruireGrilleAeAsync(
            rechargee,
            typeAe,
            item,
            request.IdGroupeItemAE,
            request.Mois,
            cancellationToken);
    }

    private async Task<GrilleImputationAeDto> ConstruireGrilleAeAsync(
        DemandePaiementEntity demande,
        TypeBudget typeAe,
        string libelleItemAE,
        long? idGroupeItemAE,
        byte? mois,
        CancellationToken cancellationToken)
    {
        var rubriques = await _repository.ListRubriquesDcActivesAsync(cancellationToken);
        var idVersion = demande.FK_VersionBudgetaire
            ?? await _repository.ResolveVersionBudgetaireValideeAsync(
                demande.FK_ExerciceBudgetaire,
                demande.FK_UniteBudgetaire,
                cancellationToken);

        IReadOnlyDictionary<long, PrevisionBudgetaire> previsions = new Dictionary<long, PrevisionBudgetaire>();
        if (idVersion is long versionId)
        {
            previsions = await _repository.GetPrevisionsAeParRubriqueAsync(
                versionId,
                demande.FK_UniteBudgetaire,
                typeAe.IdTypeBudget,
                libelleItemAE,
                cancellationToken);
        }

        var engageAnnuel = await _repository.SumEngageAeMapsAsync(
            demande.FK_ExerciceBudgetaire,
            demande.FK_UniteBudgetaire,
            demande.IdDemandePaiement,
            cancellationToken);

        var aeSurDemande = demande.Imputations.Where(EstImputationAe).ToList();

        var encoursScope = aeSurDemande
            .Where(i => string.Equals(
                (i.LibelleItemAE ?? string.Empty).Trim(),
                libelleItemAE,
                StringComparison.OrdinalIgnoreCase))
            .Where(i => i.Mois == mois)
            .Where(i => i.FK_RubriqueBudgetaire.HasValue)
            .GroupBy(i => i.FK_RubriqueBudgetaire!.Value)
            .ToDictionary(
                g => g.Key,
                g => (
                    IdImputation: (long?)g.First().IdImputation,
                    Brut: g.Sum(x => x.MontantBrut),
                    Usd: g.Sum(x => x.MontantUsd)));

        var encoursAnnuelParRb = aeSurDemande
            .Where(i => string.Equals(
                (i.LibelleItemAE ?? string.Empty).Trim(),
                libelleItemAE,
                StringComparison.OrdinalIgnoreCase))
            .Where(i => i.FK_RubriqueBudgetaire.HasValue)
            .GroupBy(i => i.FK_RubriqueBudgetaire!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));

        var lignes = new List<LigneGrilleImputationAeDto>(rubriques.Count);
        foreach (var rb in rubriques)
        {
            previsions.TryGetValue(rb.IdRB, out var prevision);
            encoursScope.TryGetValue(rb.IdRB, out var encours);
            encoursAnnuelParRb.TryGetValue(rb.IdRB, out var encoursAnnuel);
            engageAnnuel.TryGetValue(new EngageAeAnnuelCle(rb.IdRB, libelleItemAE), out var creditAnnuel);

            var budgetAnnuel = prevision?.MontantAnnuel ?? 0m;
            var encoursUsd = encours.Usd;
            var encoursBrut = encours.Brut;
            var dispoAnnuel = budgetAnnuel - creditAnnuel - encoursAnnuel;

            lignes.Add(new LigneGrilleImputationAeDto(
                rb.IdRB,
                rb.CodeRB,
                rb.Libelle,
                encoursBrut > 0m,
                encours.IdImputation,
                prevision?.IdPrevision,
                prevision is not null,
                encoursBrut,
                encoursUsd,
                encoursUsd,
                budgetAnnuel,
                creditAnnuel,
                dispoAnnuel,
                encoursAnnuel));
        }

        var existantes = aeSurDemande
            .Where(i => i.FK_RubriqueBudgetaire.HasValue)
            .OrderBy(i => i.LibelleItemAE)
            .ThenBy(i => i.Mois)
            .ThenBy(i => i.Ordre)
            .Select(i =>
            {
                var rb = rubriques.FirstOrDefault(r => r.IdRB == i.FK_RubriqueBudgetaire);
                return new LigneExistanteImputationAeDto(
                    i.IdImputation,
                    (i.LibelleItemAE ?? string.Empty).Trim(),
                    i.FK_GroupeItemAE,
                    i.Mois,
                    i.FK_RubriqueBudgetaire!.Value,
                    rb?.CodeRB ?? string.Empty,
                    rb?.Libelle ?? string.Empty,
                    i.MontantBrut,
                    i.MontantUsd);
            })
            .ToList();

        var totalBrut = aeSurDemande.Sum(i => i.MontantBrut);
        var totalUsd = aeSurDemande.Sum(i => i.MontantUsd);
        var (tauxHeader, _) = await ResoudreTauxImputationDcAsync(demande, cancellationToken);

        return new GrilleImputationAeDto(
            demande.IdDemandePaiement,
            demande.Reference,
            DemandePaiementMontants.NormaliserCodeDevise(demande.Devise),
            demande.MontantBrut,
            tauxHeader,
            demande.MontantUsd,
            demande.FK_UniteBudgetaire,
            demande.UniteBudgetaire?.CodeUB ?? string.Empty,
            demande.UniteBudgetaire?.Libelle ?? string.Empty,
            libelleItemAE,
            idGroupeItemAE,
            mois,
            previsions.Values.Sum(p => p.MontantAnnuel),
            engageAnnuel
                .Where(kv => string.Equals(kv.Key.LibelleItemAE, libelleItemAE, StringComparison.OrdinalIgnoreCase))
                .Sum(kv => kv.Value),
            lignes,
            existantes,
            totalBrut,
            demande.MontantBrut - totalBrut,
            totalUsd,
            (demande.MontantUsd ?? 0m) - totalUsd);
    }

    private static void ExigerTypeBudgetAe(DemandePaiementEntity demande)
    {
        var code = demande.TypeBudget?.CodeType?.Trim().ToUpperInvariant();
        if (!string.Equals(code, TypeBudgetCode.ActionsExploitation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La grille d'imputation AE est réservée aux demandes orientées Actions d'exploitation.");
        }
    }

    private static void ExigerItemAe(string? libelleItemAE)
    {
        if (string.IsNullOrWhiteSpace(libelleItemAE))
            throw new ArgumentException("L'Item AE est obligatoire.");
    }

    private static void ExigerMoisVentilationAe(byte? mois)
    {
        if (mois is < 1 or > 12)
            throw new ArgumentException("Le mois de ventilation doit être compris entre 1 et 12.");
    }

    private static bool EstImputationAe(DemandePaiementImputation i)
        => !string.IsNullOrWhiteSpace(i.LibelleItemAE)
           && i.FK_RubriqueBudgetaire.HasValue
           && !i.FK_ItemBI.HasValue
           && string.IsNullOrWhiteSpace(i.DetailBI);
}
