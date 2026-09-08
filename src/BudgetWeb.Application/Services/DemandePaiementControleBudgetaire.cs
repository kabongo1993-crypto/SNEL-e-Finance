using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

/// <summary>
/// Contrôle budgétaire DPM — consomme PREVISION_BUDGETAIRE sans la modifier.
/// L'absence de prévision (Prévision = 0) n'est jamais un motif de rejet isolé.
/// </summary>
public static class DemandePaiementControleBudgetaire
{
    /// <summary>Crédits engagés / encours pré-chargés (tests ou batch). Si null, interroge le repository.</summary>
    public sealed record DonneesEngagementPrechargees(
        decimal? CreditEngageMensuel = null,
        decimal? CreditEngageAnnuel = null,
        decimal? EngagementEnCours = null,
        decimal? EngagementEnCoursAnnuel = null);

    public static async Task<ControleImputationDto> ControleImputationAsync(
        DemandePaiementImputation imputation,
        string codeTypeBudget,
        PrevisionBudgetaire? prevision,
        long? excludeDemandeId,
        IDemandePaiementRepository repository,
        DonneesEngagementPrechargees? donneesPrechargees = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imputation);
        ArgumentNullException.ThrowIfNull(repository);

        var code = (codeTypeBudget ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
            code = imputation.TypeBudget?.CodeType?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(code))
            throw new ArgumentException("Le code type budget est obligatoire pour le contrôle budgétaire.");

        var montantCourantUsd = imputation.MontantUsd;
        var engagementEnCours = donneesPrechargees?.EngagementEnCours ?? montantCourantUsd;
        var engagementEnCoursAnnuel = donneesPrechargees?.EngagementEnCoursAnnuel ?? engagementEnCours;
        var budgetAnnuel = prevision?.MontantAnnuel ?? 0m;
        var montantPrevision = prevision?.MontantAnnuel ?? 0m;
        var ecartPrevisionImputation = montantPrevision - montantCourantUsd;

        decimal creditEngageAnnuel;
        decimal? creditEngageMensuel = null;
        decimal? budgetMensuel = null;
        decimal? creditDisponibleMensuel = null;

        switch (code)
        {
            case TypeBudgetCode.DepensesCourantes:
            {
                if (!imputation.Mois.HasValue || !imputation.FK_RubriqueBudgetaire.HasValue)
                    throw new ArgumentException("Imputation DC incomplète pour le contrôle budgétaire.");

                creditEngageMensuel = donneesPrechargees?.CreditEngageMensuel
                    ?? await repository.SumEngageDcMensuelAsync(
                        imputation.FK_ExerciceBudgetaire,
                        imputation.FK_UniteBudgetaire,
                        imputation.FK_RubriqueBudgetaire.Value,
                        imputation.Mois.Value,
                        excludeDemandeId,
                        cancellationToken);

                creditEngageAnnuel = donneesPrechargees?.CreditEngageAnnuel
                    ?? await repository.SumEngageDcAnnuelAsync(
                        imputation.FK_ExerciceBudgetaire,
                        imputation.FK_UniteBudgetaire,
                        imputation.FK_RubriqueBudgetaire.Value,
                        excludeDemandeId,
                        cancellationToken);

                budgetMensuel = CalculerBudgetMensuel(prevision, imputation.Mois.Value);
                creditDisponibleMensuel = budgetMensuel.Value - creditEngageMensuel.Value - engagementEnCours;
                break;
            }
            case TypeBudgetCode.ActionsExploitation:
            {
                if (!imputation.FK_RubriqueBudgetaire.HasValue
                    || string.IsNullOrWhiteSpace(imputation.LibelleItemAE))
                    throw new ArgumentException("Imputation AE incomplète pour le contrôle budgétaire.");

                creditEngageAnnuel = donneesPrechargees?.CreditEngageAnnuel
                    ?? await repository.SumEngageAeAnnuelAsync(
                        imputation.FK_ExerciceBudgetaire,
                        imputation.FK_UniteBudgetaire,
                        imputation.FK_RubriqueBudgetaire.Value,
                        imputation.LibelleItemAE.Trim(),
                        excludeDemandeId,
                        cancellationToken);
                break;
            }
            case TypeBudgetCode.BudgetInvestissement:
            {
                if (!imputation.FK_ItemBI.HasValue
                    || string.IsNullOrWhiteSpace(imputation.DetailBI))
                    throw new ArgumentException("Imputation BI incomplète pour le contrôle budgétaire.");

                creditEngageAnnuel = donneesPrechargees?.CreditEngageAnnuel
                    ?? await repository.SumEngageBiAnnuelAsync(
                        imputation.FK_ExerciceBudgetaire,
                        imputation.FK_UniteBudgetaire,
                        imputation.FK_ItemBI.Value,
                        imputation.DetailBI!.Trim(),
                        excludeDemandeId,
                        cancellationToken);
                break;
            }
            default:
                throw new ArgumentException($"Code type budget inconnu pour le contrôle : {codeTypeBudget}.");
        }

        var creditDisponibleAnnuel = budgetAnnuel - creditEngageAnnuel - engagementEnCoursAnnuel;
        var depassementMensuel = creditDisponibleMensuel is decimal dispoM && dispoM < 0m;
        var depassementAnnuel = creditDisponibleAnnuel < 0m;
        var (estValide, motifRejet) = EvaluerValidite(code, creditDisponibleAnnuel);

        return new ControleImputationDto(
            imputation.IdImputation,
            imputation.Ordre,
            code,
            estValide,
            motifRejet,
            budgetAnnuel,
            budgetMensuel,
            creditEngageAnnuel,
            creditEngageMensuel,
            engagementEnCours,
            creditDisponibleAnnuel,
            creditDisponibleMensuel,
            montantPrevision,
            ecartPrevisionImputation,
            montantCourantUsd,
            imputation.FK_BudgetLigne,
            depassementMensuel,
            depassementAnnuel);
    }

    public static async Task<ControleBudgetaireDto> ControlerDemandeAsync(
        DemandePaiementEntity demande,
        IDemandePaiementRepository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        if (demande.Imputations.Count == 0)
        {
            return new ControleBudgetaireDto(
                false,
                "Aucune imputation budgétaire à contrôler.",
                []);
        }

        var imputations = demande.Imputations.OrderBy(i => i.Ordre).ToList();
        var controles = new List<ControleImputationDto>(imputations.Count);

        foreach (var imputation in imputations)
        {
            var codeType = imputation.TypeBudget?.CodeType
                ?? (await repository.GetTypeBudgetAsync(imputation.FK_TypeBudget, cancellationToken))?.CodeType
                ?? throw new InvalidOperationException(
                    $"Type budget introuvable pour l'imputation {imputation.Ordre}.");

            var prevision = await ResoudrePrevisionAsync(
                imputation,
                demande.FK_VersionBudgetaire,
                repository,
                cancellationToken);

            DonneesEngagementPrechargees? prechargees = null;
            if (string.Equals(codeType, TypeBudgetCode.DepensesCourantes, StringComparison.Ordinal))
            {
                var encoursMensuel = demande.Imputations
                    .Where(x => x.FK_ExerciceBudgetaire == imputation.FK_ExerciceBudgetaire
                                && x.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                                && x.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire
                                && x.Mois == imputation.Mois)
                    .Sum(x => x.MontantUsd);
                var encoursAnnuel = demande.Imputations
                    .Where(x => x.FK_ExerciceBudgetaire == imputation.FK_ExerciceBudgetaire
                                && x.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                                && x.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire
                                && x.Mois != null)
                    .Sum(x => x.MontantUsd);
                prechargees = new DonneesEngagementPrechargees(
                    EngagementEnCours: encoursMensuel,
                    EngagementEnCoursAnnuel: encoursAnnuel);
            }
            else if (string.Equals(codeType, TypeBudgetCode.ActionsExploitation, StringComparison.Ordinal))
            {
                var item = (imputation.LibelleItemAE ?? string.Empty).Trim();
                var encoursAnnuel = demande.Imputations
                    .Where(x => x.FK_ExerciceBudgetaire == imputation.FK_ExerciceBudgetaire
                                && x.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                                && x.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire
                                && string.Equals(
                                    (x.LibelleItemAE ?? string.Empty).Trim(),
                                    item,
                                    StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.MontantUsd);
                prechargees = new DonneesEngagementPrechargees(
                    EngagementEnCours: encoursAnnuel,
                    EngagementEnCoursAnnuel: encoursAnnuel);
            }
            else if (string.Equals(codeType, TypeBudgetCode.BudgetInvestissement, StringComparison.Ordinal))
            {
                var detail = (imputation.DetailBI ?? string.Empty).Trim();
                var encoursAnnuel = demande.Imputations
                    .Where(x => x.FK_ExerciceBudgetaire == imputation.FK_ExerciceBudgetaire
                                && x.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                                && x.FK_ItemBI == imputation.FK_ItemBI)
                    .Where(x => string.Equals(
                        (x.DetailBI ?? string.Empty).Trim(),
                        detail,
                        StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.MontantUsd);
                prechargees = new DonneesEngagementPrechargees(
                    EngagementEnCours: encoursAnnuel,
                    EngagementEnCoursAnnuel: encoursAnnuel);
            }

            controles.Add(await ControleImputationAsync(
                imputation,
                codeType,
                prevision,
                demande.IdDemandePaiement,
                repository,
                prechargees,
                cancellationToken));
        }

        var estValide = controles.All(c => c.EstValide);
        string? motifGlobal = null;
        if (!estValide)
        {
            motifGlobal = string.Join(
                " ",
                controles
                    .Where(c => !c.EstValide)
                    .Select(c => $"Imputation {c.Ordre} : {c.MotifRejet}"));
        }

        return new ControleBudgetaireDto(estValide, motifGlobal, controles);
    }

    public static DemandePaiementImputationSnapshot CreerSnapshot(
        DemandePaiementImputation imputation,
        long idDemande,
        ControleImputationDto controle,
        DateTime dateSnapshot)
        => new()
        {
            FK_Imputation = imputation.IdImputation,
            FK_DemandePaiement = idDemande,
            DateSnapshot = dateSnapshot,
            BudgetMensuel = controle.BudgetMensuel,
            CreditEngageMensuel = controle.CreditEngageMensuel,
            CreditDisponibleMensuelAvantVisa = controle.CreditDisponibleMensuel,
            BudgetAnnuel = controle.BudgetAnnuel,
            CreditEngageAnnuel = controle.CreditEngageAnnuel,
            CreditDisponibleAnnuelAvantVisa = controle.CreditDisponibleAnnuel,
            MontantPrevision = controle.MontantPrevision,
            EcartPrevisionImputation = controle.EcartPrevisionImputation,
            MontantBrut = imputation.MontantBrut,
            Devise = imputation.Devise,
            TauxConversion = imputation.TauxConversion,
            MontantUsd = imputation.MontantUsd,
            FK_BudgetLigne = imputation.FK_BudgetLigne,
        };

    private static async Task<PrevisionBudgetaire?> ResoudrePrevisionAsync(
        DemandePaiementImputation imputation,
        long? idVersion,
        IDemandePaiementRepository repository,
        CancellationToken cancellationToken)
    {
        if (imputation.FK_BudgetLigne is long idLigne)
            return await repository.GetPrevisionAsync(idLigne, cancellationToken);

        if (idVersion is not long versionId)
            return null;

        var idPrevision = await repository.FindPrevisionIdAsync(imputation, versionId, cancellationToken);
        if (idPrevision is not long idPrev)
            return null;

        imputation.FK_BudgetLigne = idPrev;
        return await repository.GetPrevisionAsync(idPrev, cancellationToken);
    }

    public static decimal CalculerBudgetMensuel(PrevisionBudgetaire? prevision, byte mois)
    {
        if (prevision is null)
            return 0m;

        var mode = prevision.ModePrevision?.CodeMode?.Trim().ToUpperInvariant()
            ?? ModePrevisionCode.Annuel;

        if (string.Equals(mode, ModePrevisionCode.Mensuel, StringComparison.Ordinal))
        {
            var repartition = prevision.RepartitionsMensuelles
                .FirstOrDefault(r => r.Mois == mois);
            return repartition?.Montant ?? 0m;
        }

        return prevision.MontantAnnuel / 12m;
    }

    private static (bool EstValide, string? MotifRejet) EvaluerValidite(
        string codeTypeBudget,
        decimal creditDisponibleAnnuel)
    {
        // DC : le dépassement de crédit est affiché, il ne bloque pas le visa.
        if (string.Equals(codeTypeBudget, TypeBudgetCode.DepensesCourantes, StringComparison.Ordinal))
            return (true, null);

        var motifs = new List<string>();

        if (creditDisponibleAnnuel < 0m)
            motifs.Add("Le crédit disponible annuel est insuffisant.");

        if (motifs.Count == 0)
            return (true, null);

        return (false, string.Join(" ", motifs));
    }
}
