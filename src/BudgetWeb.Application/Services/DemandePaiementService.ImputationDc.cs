using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public async Task<GrilleImputationDcDto> GetGrilleImputationDcAsync(
        long idDemande,
        byte? mois = null,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);
        ExigerTypeBudgetDc(demande);

        var typeDc = await _repository.GetTypeBudgetByCodeAsync(TypeBudgetCode.DepensesCourantes, cancellationToken)
            ?? throw new InvalidOperationException("Type budget DC introuvable.");

        var moisEffectif = ResoudreMoisGrilleDc(demande, mois);
        return await ConstruireGrilleDcAsync(demande, typeDc, moisEffectif, cancellationToken);
    }

    public async Task<GrilleImputationDcDto> EnregistrerImputationsDcAsync(
        long idDemande,
        EnregistrerImputationsDcRequest request,
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

        var typeDc = await _repository.GetTypeBudgetByCodeAsync(TypeBudgetCode.DepensesCourantes, cancellationToken)
            ?? throw new InvalidOperationException("Type budget DC introuvable.");

        ExigerImputer(typeDc.CodeType);
        ExigerTypeBudgetDemande(demande, typeDc.IdTypeBudget, typeDc.CodeType);
        ExigerMoisDc(request.Mois);

        var lignes = request.Lignes ?? [];
        var ids = new HashSet<long>();
        foreach (var ligne in lignes)
        {
            if (ligne.IdRubriqueBudgetaire <= 0)
                throw new ArgumentException("La rubrique budgétaire est obligatoire.");
            if (ligne.MontantBrut < 0m)
                throw new ArgumentException("Le montant d'imputation ne peut pas être négatif.");
            if (!ids.Add(ligne.IdRubriqueBudgetaire))
                throw new ArgumentException("Une même rubrique ne peut être imputée qu'une fois pour le mois.");
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
            .Where(i => !(i.Mois == request.Mois && EstImputationDc(i)))
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
                FK_TypeBudget = typeDc.IdTypeBudget,
                FK_UniteBudgetaire = demande.FK_UniteBudgetaire,
                FK_ExerciceBudgetaire = demande.FK_ExerciceBudgetaire,
                FK_RubriqueBudgetaire = ligne.IdRubriqueBudgetaire,
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
            DemandePaiementImputationRules.ValiderCoherenceTypeBudget(typeDc.CodeType, imputation);
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

        await _repository.ReplaceImputationsDcForMoisAsync(idDemande, request.Mois, nouvelles, cancellationToken);
        await _repository.AddAuditAsync(
            userId,
            "IMPUTER",
            idDemande,
            null,
            new
            {
                mois = request.Mois,
                nbLignes = nouvelles.Count,
                totalUsd = nouvelles.Sum(i => i.MontantUsd),
            },
            cancellationToken);

        var rechargee = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");
        return await ConstruireGrilleDcAsync(rechargee, typeDc, request.Mois, cancellationToken);
    }

    private async Task<GrilleImputationDcDto> ConstruireGrilleDcAsync(
        DemandePaiementEntity demande,
        TypeBudget typeDc,
        byte mois,
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
            previsions = await _repository.GetPrevisionsDcParRubriqueAsync(
                versionId,
                demande.FK_UniteBudgetaire,
                typeDc.IdTypeBudget,
                cancellationToken);
        }

        var (engageMensuel, engageAnnuel) = await _repository.SumEngageDcMapsAsync(
            demande.FK_ExerciceBudgetaire,
            demande.FK_UniteBudgetaire,
            demande.IdDemandePaiement,
            cancellationToken);

        var encoursParRb = demande.Imputations
            .Where(EstImputationDc)
            .Where(i => i.Mois == mois)
            .Where(i => i.FK_RubriqueBudgetaire.HasValue)
            .GroupBy(i => i.FK_RubriqueBudgetaire!.Value)
            .ToDictionary(
                g => g.Key,
                g => (
                    IdImputation: (long?)g.First().IdImputation,
                    Brut: g.Sum(x => x.MontantBrut),
                    Usd: g.Sum(x => x.MontantUsd)));

        var encoursAnnuelParRb = demande.Imputations
            .Where(EstImputationDc)
            .Where(i => i.FK_RubriqueBudgetaire.HasValue)
            .GroupBy(i => i.FK_RubriqueBudgetaire!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));

        var lignes = new List<LigneGrilleImputationDcDto>(rubriques.Count);
        foreach (var rb in rubriques)
        {
            previsions.TryGetValue(rb.IdRB, out var prevision);
            encoursParRb.TryGetValue(rb.IdRB, out var encours);
            encoursAnnuelParRb.TryGetValue(rb.IdRB, out var encoursAnnuel);
            engageMensuel.TryGetValue(new EngageDcMensuelCle(rb.IdRB, mois), out var creditMensuel);
            engageAnnuel.TryGetValue(rb.IdRB, out var creditAnnuel);

            var budgetAnnuel = prevision?.MontantAnnuel ?? 0m;
            var budgetMensuel = DemandePaiementControleBudgetaire.CalculerBudgetMensuel(prevision, mois);
            var encoursUsd = encours.Usd;
            var encoursBrut = encours.Brut;
            var dispoMensuel = budgetMensuel - creditMensuel - encoursUsd;
            var dispoAnnuel = budgetAnnuel - creditAnnuel - encoursAnnuel;

            lignes.Add(new LigneGrilleImputationDcDto(
                rb.IdRB,
                rb.CodeRB,
                rb.Libelle,
                encoursBrut > 0m,
                encours.IdImputation,
                prevision?.IdPrevision,
                prevision is not null,
                budgetMensuel,
                creditMensuel,
                encoursBrut,
                encoursUsd,
                encoursUsd,
                dispoMensuel,
                budgetAnnuel,
                creditAnnuel,
                dispoAnnuel,
                encoursAnnuel));
        }

        var totalBrut = lignes.Sum(l => l.EngagementEnCoursBrut);
        var totalUsd = lignes.Sum(l => l.EngagementEnCoursUsd);
        var (tauxHeader, _) = await ResoudreTauxImputationDcAsync(demande, cancellationToken);

        return new GrilleImputationDcDto(
            demande.IdDemandePaiement,
            demande.Reference,
            DemandePaiementMontants.NormaliserCodeDevise(demande.Devise),
            demande.MontantBrut,
            tauxHeader,
            demande.MontantUsd,
            demande.FK_UniteBudgetaire,
            demande.UniteBudgetaire?.CodeUB ?? string.Empty,
            demande.UniteBudgetaire?.Libelle ?? string.Empty,
            mois,
            previsions.Values.Sum(p => p.MontantAnnuel),
            engageAnnuel.Values.Sum(),
            lignes,
            totalBrut,
            demande.MontantBrut - totalBrut,
            totalUsd,
            (demande.MontantUsd ?? 0m) - totalUsd);
    }

    private async Task<(decimal Taux, long? IdTaux)> ResoudreTauxImputationDcAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        if (demande.TauxConversion is decimal tauxHeader && tauxHeader > 0m)
            return (tauxHeader, demande.FK_TauxChange);

        DateOnly dateReference;
        if (BilletConversionRules.NecessiteBillet(demande.ModePaiementSollicite, demande.Devise))
        {
            var billet = await _repository.GetBilletConversionByDemandeAsync(
                demande.IdDemandePaiement,
                cancellationToken);
            dateReference = billet?.DateConversion ?? DateTraitementDpm();
        }
        else
        {
            dateReference = DateTraitementDpm();
        }

        var conversion = await AppliquerTauxAsync(
            demande.MontantBrut > 0m ? demande.MontantBrut : 1m,
            demande.Devise,
            dateReference,
            cancellationToken);
        return (conversion.Taux, conversion.IdTaux);
    }

    private static byte ResoudreMoisGrilleDc(DemandePaiementEntity demande, byte? mois)
    {
        if (mois is byte demandeMois)
        {
            ExigerMoisDc(demandeMois);
            return demandeMois;
        }

        var moisExistants = demande.Imputations
            .Where(EstImputationDc)
            .Select(i => i.Mois!.Value)
            .Distinct()
            .ToList();

        return moisExistants.Count == 1
            ? moisExistants[0]
            : (byte)DateTime.Now.Month;
    }

    private static void ExigerTypeBudgetDc(DemandePaiementEntity demande)
    {
        var code = demande.TypeBudget?.CodeType?.Trim().ToUpperInvariant();
        if (!string.Equals(code, TypeBudgetCode.DepensesCourantes, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La grille d'imputation DC est réservée aux demandes orientées Dépenses courantes.");
        }
    }

    private static void ExigerMoisDc(byte mois)
    {
        if (mois is < 1 or > 12)
            throw new ArgumentException("Le mois doit être compris entre 1 et 12.");
    }

    private static bool EstImputationDc(DemandePaiementImputation i)
        => i.Mois.HasValue
           && i.FK_RubriqueBudgetaire.HasValue
           && string.IsNullOrWhiteSpace(i.LibelleItemAE)
           && !i.FK_GroupeItemAE.HasValue
           && !i.FK_ItemBI.HasValue
           && string.IsNullOrWhiteSpace(i.DetailBI);
}
