using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.DetailBI;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public async Task<GrilleImputationBiDto> GetGrilleImputationBiAsync(
        long idDemande,
        long idItemBI,
        byte? mois = null,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);
        ExigerTypeBudgetBi(demande);
        ExigerItemBi(idItemBI);
        ExigerMoisVentilationBi(mois);

        var typeBi = await _repository.GetTypeBudgetByCodeAsync(TypeBudgetCode.BudgetInvestissement, cancellationToken)
            ?? throw new InvalidOperationException("Type budget BI introuvable.");

        return await ConstruireGrilleBiAsync(
            demande,
            typeBi,
            idItemBI,
            mois,
            cancellationToken);
    }

    public async Task<GrilleImputationBiDto> EnregistrerImputationsBiAsync(
        long idDemande,
        EnregistrerImputationsBiRequest request,
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

        var typeBi = await _repository.GetTypeBudgetByCodeAsync(TypeBudgetCode.BudgetInvestissement, cancellationToken)
            ?? throw new InvalidOperationException("Type budget BI introuvable.");

        ExigerImputer(typeBi.CodeType);
        ExigerTypeBudgetDemande(demande, typeBi.IdTypeBudget, typeBi.CodeType);
        ExigerItemBi(request.IdItemBI);
        ExigerMoisVentilationBi(request.Mois);

        var lignes = request.Lignes ?? [];
        var cles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ligne in lignes)
        {
            var detail = DetailBILibelle.Normaliser(ligne.DetailBI);
            if (string.IsNullOrEmpty(detail))
                throw new ArgumentException("Le détail BI est obligatoire.");
            if (ligne.MontantBrut < 0m)
                throw new ArgumentException("Le montant d'imputation ne peut pas être négatif.");
            if (!cles.Add(detail))
                throw new ArgumentException("Un même détail ne peut être imputé qu'une fois pour l'item et le mois.");
        }

        var (taux, _) = await ResoudreTauxImputationDcAsync(demande, cancellationToken);
        var devise = DemandePaiementMontants.NormaliserCodeDevise(demande.Devise);
        var idVersion = demande.FK_VersionBudgetaire
            ?? await _repository.ResolveVersionBudgetaireValideeAsync(
                demande.FK_ExerciceBudgetaire,
                demande.FK_UniteBudgetaire,
                cancellationToken);

        var aConserver = lignes
            .Select(l => (Detail: DetailBILibelle.Normaliser(l.DetailBI), l.MontantBrut))
            .Where(l => l.MontantBrut > 0m && !string.IsNullOrEmpty(l.Detail))
            .ToList();

        var maxOrdreAutres = demande.Imputations
            .Where(i => !(EstImputationBi(i)
                          && i.FK_ItemBI == request.IdItemBI
                          && i.Mois == request.Mois))
            .Select(i => i.Ordre)
            .DefaultIfEmpty(0)
            .Max();

        var nouvelles = new List<DemandePaiementImputation>(aConserver.Count);
        var ordre = maxOrdreAutres;
        foreach (var ligne in aConserver.OrderBy(l => l.Detail, StringComparer.OrdinalIgnoreCase))
        {
            ordre++;
            var montantUsd = DemandePaiementMontants.CalculerMontantUsd(ligne.MontantBrut, taux);
            var imputation = new DemandePaiementImputation
            {
                FK_DemandePaiement = idDemande,
                Ordre = ordre,
                FK_TypeBudget = typeBi.IdTypeBudget,
                FK_UniteBudgetaire = demande.FK_UniteBudgetaire,
                FK_ExerciceBudgetaire = demande.FK_ExerciceBudgetaire,
                FK_ItemBI = request.IdItemBI,
                DetailBI = ligne.Detail,
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
            DemandePaiementImputationRules.ValiderCoherenceTypeBudget(typeBi.CodeType, imputation);
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

        await _repository.ReplaceImputationsBiForItemMoisAsync(
            idDemande,
            request.IdItemBI,
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
                filiere = "BI",
                idItemBI = request.IdItemBI,
                mois = request.Mois,
                nbLignes = nouvelles.Count,
                totalUsd = nouvelles.Sum(i => i.MontantUsd),
            },
            cancellationToken);

        var rechargee = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");
        return await ConstruireGrilleBiAsync(
            rechargee,
            typeBi,
            request.IdItemBI,
            request.Mois,
            cancellationToken);
    }

    private async Task<GrilleImputationBiDto> ConstruireGrilleBiAsync(
        DemandePaiementEntity demande,
        TypeBudget typeBi,
        long idItemBI,
        byte? mois,
        CancellationToken cancellationToken)
    {
        var item = await _itemBIRepository.GetByIdAsync(idItemBI, cancellationToken)
            ?? throw new ArgumentException("Item BI introuvable.");

        var idVersion = demande.FK_VersionBudgetaire
            ?? await _repository.ResolveVersionBudgetaireValideeAsync(
                demande.FK_ExerciceBudgetaire,
                demande.FK_UniteBudgetaire,
                cancellationToken);

        IReadOnlyDictionary<string, PrevisionBudgetaire> previsions =
            new Dictionary<string, PrevisionBudgetaire>(StringComparer.OrdinalIgnoreCase);
        if (idVersion is long versionIdPrev)
        {
            previsions = await _repository.GetPrevisionsBiParDetailAsync(
                versionIdPrev,
                demande.FK_UniteBudgetaire,
                typeBi.IdTypeBudget,
                idItemBI,
                cancellationToken);
        }

        var engageAnnuel = await _repository.SumEngageBiMapsAsync(
            demande.FK_ExerciceBudgetaire,
            demande.FK_UniteBudgetaire,
            demande.IdDemandePaiement,
            cancellationToken);

        var biSurDemande = demande.Imputations.Where(EstImputationBi).ToList();

        var detailLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in previsions)
            detailLabels.TryAdd(p.Key, p.Value.DetailBI ?? p.Key);
        foreach (var imp in biSurDemande.Where(i => i.FK_ItemBI == idItemBI && !string.IsNullOrWhiteSpace(i.DetailBI)))
        {
            var cle = DetailBILibelle.Normaliser(imp.DetailBI);
            if (!string.IsNullOrEmpty(cle))
                detailLabels.TryAdd(cle, DetailBILibelle.Normaliser(imp.DetailBI!));
        }

        var encoursScope = biSurDemande
            .Where(i => i.FK_ItemBI == idItemBI && i.Mois == mois)
            .Where(i => !string.IsNullOrWhiteSpace(i.DetailBI))
            .GroupBy(i => DetailBILibelle.Normaliser(i.DetailBI!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (
                    IdImputation: (long?)g.First().IdImputation,
                    Brut: g.Sum(x => x.MontantBrut),
                    Usd: g.Sum(x => x.MontantUsd)),
                StringComparer.OrdinalIgnoreCase);

        var encoursAnnuelParDetail = biSurDemande
            .Where(i => i.FK_ItemBI == idItemBI)
            .Where(i => !string.IsNullOrWhiteSpace(i.DetailBI))
            .GroupBy(i => DetailBILibelle.Normaliser(i.DetailBI!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd), StringComparer.OrdinalIgnoreCase);

        var lignes = new List<LigneGrilleImputationBiDto>(detailLabels.Count);
        var ordre = 0;
        foreach (var detail in detailLabels.Values.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            ordre++;
            var cle = DetailBILibelle.Normaliser(detail);
            previsions.TryGetValue(cle, out var prevision);
            encoursScope.TryGetValue(cle, out var encours);
            encoursAnnuelParDetail.TryGetValue(cle, out var encoursAnnuel);
            engageAnnuel.TryGetValue(new EngageBiAnnuelCle(idItemBI, cle), out var creditAnnuel);

            var budgetAnnuel = prevision?.MontantAnnuel ?? 0m;
            var encoursBrut = encours.Brut;
            var encoursUsd = encours.Usd;
            var dispoAnnuel = budgetAnnuel - creditAnnuel - encoursAnnuel;

            lignes.Add(new LigneGrilleImputationBiDto(
                detail,
                ordre,
                detail,
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

        var existantes = biSurDemande
            .Where(i => !string.IsNullOrWhiteSpace(i.DetailBI))
            .OrderBy(i => i.FK_ItemBI)
            .ThenBy(i => i.Mois)
            .ThenBy(i => i.Ordre)
            .Select(i => new LigneExistanteImputationBiDto(
                i.IdImputation,
                i.FK_ItemBI!.Value,
                i.FK_ItemBI == idItemBI ? item.Libelle : (i.ItemBI?.Libelle ?? string.Empty),
                DetailBILibelle.Normaliser(i.DetailBI!),
                i.Mois,
                i.MontantBrut,
                i.MontantUsd))
            .ToList();

        var totalBrut = biSurDemande.Sum(i => i.MontantBrut);
        var totalUsd = biSurDemande.Sum(i => i.MontantUsd);
        var (tauxHeader, _) = await ResoudreTauxImputationDcAsync(demande, cancellationToken);

        return new GrilleImputationBiDto(
            demande.IdDemandePaiement,
            demande.Reference,
            DemandePaiementMontants.NormaliserCodeDevise(demande.Devise),
            demande.MontantBrut,
            tauxHeader,
            demande.MontantUsd,
            demande.FK_UniteBudgetaire,
            demande.UniteBudgetaire?.CodeUB ?? string.Empty,
            demande.UniteBudgetaire?.Libelle ?? string.Empty,
            idItemBI,
            item.CodeItem,
            item.Libelle,
            mois,
            previsions.Values.Sum(p => p.MontantAnnuel),
            engageAnnuel
                .Where(kv => kv.Key.IdItemBI == idItemBI)
                .Sum(kv => kv.Value),
            lignes,
            existantes,
            totalBrut,
            demande.MontantBrut - totalBrut,
            totalUsd,
            (demande.MontantUsd ?? 0m) - totalUsd);
    }

    private static void ExigerTypeBudgetBi(DemandePaiementEntity demande)
    {
        var code = demande.TypeBudget?.CodeType?.Trim().ToUpperInvariant();
        if (!string.Equals(code, TypeBudgetCode.BudgetInvestissement, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La grille d'imputation BI est réservée aux demandes orientées Budget d'investissement.");
        }
    }

    private static void ExigerItemBi(long idItemBI)
    {
        if (idItemBI <= 0)
            throw new ArgumentException("L'Item BI est obligatoire.");
    }

    private static void ExigerMoisVentilationBi(byte? mois)
    {
        if (mois is < 1 or > 12)
            throw new ArgumentException("Le mois de ventilation doit être compris entre 1 et 12.");
    }

    private static bool EstImputationBi(DemandePaiementImputation i)
        => i.FK_ItemBI.HasValue
           && !string.IsNullOrWhiteSpace(i.DetailBI)
           && !i.FK_RubriqueBudgetaire.HasValue
           && string.IsNullOrWhiteSpace(i.LibelleItemAE);
}
