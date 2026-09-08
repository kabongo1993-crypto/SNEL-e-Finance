using BudgetWeb.Domain.Entities;

using BudgetWeb.Domain.Enums;



namespace BudgetWeb.Domain.DemandePaiement;



/// <summary>

/// Invariants structurels d'imputation vérifiables sans accès base.

/// La cohérence FK_TypeBudget ↔ CodeType (DC/AE/BI) reste applicative.

/// FK_BudgetLigne nullable : une imputation sans prévision est autorisée.

/// </summary>

public static class DemandePaiementImputationRules

{

    public static FormeImputationBudgetaire DetecterForme(DemandePaiementImputation imputation)

    {

        ArgumentNullException.ThrowIfNull(imputation);



        var formes = new List<FormeImputationBudgetaire>();



        if (EstFormeDc(imputation))

            formes.Add(FormeImputationBudgetaire.DepensesCourantes);



        if (EstFormeAe(imputation))

            formes.Add(FormeImputationBudgetaire.ActionsExploitation);



        if (EstFormeBi(imputation))

            formes.Add(FormeImputationBudgetaire.BudgetInvestissement);



        return formes.Count switch

        {

            1 => formes[0],

            0 => throw new ArgumentException("L'imputation ne correspond à aucune forme DC, AE ou BI."),

            _ => throw new ArgumentException("L'imputation mélange des champs incompatibles entre DC, AE et BI.")

        };

    }



    public static void ValiderStructure(DemandePaiementImputation imputation)

    {

        ArgumentNullException.ThrowIfNull(imputation);



        if (imputation.Ordre < 1)

            throw new ArgumentException("L'ordre d'imputation doit être >= 1.", nameof(imputation));



        DemandePaiementMontants.Valider(

            imputation.MontantBrut,

            imputation.Devise,

            imputation.TauxConversion,

            imputation.MontantUsd);



        if (imputation.Mois is < 1 or > 12)

            throw new ArgumentException("Le mois doit être compris entre 1 et 12.", nameof(imputation));



        var forme = DetecterForme(imputation);



        switch (forme)

        {

            case FormeImputationBudgetaire.DepensesCourantes:

                ValiderFormeDc(imputation);

                break;

            case FormeImputationBudgetaire.ActionsExploitation:

                ValiderFormeAe(imputation);

                break;

            case FormeImputationBudgetaire.BudgetInvestissement:

                ValiderFormeBi(imputation);

                break;

        }

    }



    /// <summary>

    /// Vérifie la cohérence entre la forme détectée et le code type budget, si celui-ci est connu.

    /// </summary>

    public static void ValiderCoherenceTypeBudget(string? codeTypeBudget, DemandePaiementImputation imputation)

    {

        if (string.IsNullOrWhiteSpace(codeTypeBudget))

            return;



        var forme = DetecterForme(imputation);

        var code = codeTypeBudget.Trim().ToUpperInvariant();



        var attendu = code switch

        {

            TypeBudgetCode.DepensesCourantes => FormeImputationBudgetaire.DepensesCourantes,

            TypeBudgetCode.ActionsExploitation => FormeImputationBudgetaire.ActionsExploitation,

            TypeBudgetCode.BudgetInvestissement => FormeImputationBudgetaire.BudgetInvestissement,

            _ => throw new ArgumentException($"Code type budget inconnu : {codeTypeBudget}.", nameof(codeTypeBudget))

        };



        if (forme != attendu)

            throw new ArgumentException(

                $"Incohérence : le type budget {code} ne correspond pas à la forme d'imputation {forme}.");

    }



    private static bool EstFormeDc(DemandePaiementImputation i)

        => i.Mois.HasValue

           && i.FK_RubriqueBudgetaire.HasValue

           && string.IsNullOrWhiteSpace(i.LibelleItemAE)

           && !i.FK_GroupeItemAE.HasValue

           && !i.FK_ItemBI.HasValue

           && string.IsNullOrWhiteSpace(i.DetailBI);



    private static bool EstFormeAe(DemandePaiementImputation i)

        => !string.IsNullOrWhiteSpace(i.LibelleItemAE)

           && i.FK_RubriqueBudgetaire.HasValue

           && !i.FK_ItemBI.HasValue

           && string.IsNullOrWhiteSpace(i.DetailBI);



    private static bool EstFormeBi(DemandePaiementImputation i)

        => i.FK_ItemBI.HasValue

           && !string.IsNullOrWhiteSpace(i.DetailBI)

           && !i.FK_RubriqueBudgetaire.HasValue

           && string.IsNullOrWhiteSpace(i.LibelleItemAE)

           && !i.FK_GroupeItemAE.HasValue;



    private static void ValiderFormeDc(DemandePaiementImputation i)

    {

        if (!i.FK_RubriqueBudgetaire.HasValue)

            throw new ArgumentException("La rubrique budgétaire est obligatoire pour une imputation DC.");



        if (!i.Mois.HasValue)

            throw new ArgumentException("Le mois est obligatoire pour une imputation DC.");



        if (!string.IsNullOrWhiteSpace(i.LibelleItemAE))

            throw new ArgumentException("LibelleItemAE doit être absent pour une imputation DC.");



        if (i.FK_GroupeItemAE.HasValue)

            throw new ArgumentException("FK_GroupeItemAE doit être absent pour une imputation DC.");



        if (i.FK_ItemBI.HasValue)

            throw new ArgumentException("FK_ItemBI doit être absent pour une imputation DC.");



        if (!string.IsNullOrWhiteSpace(i.DetailBI))

            throw new ArgumentException("DetailBI doit être absent pour une imputation DC.");

    }



    private static void ValiderFormeAe(DemandePaiementImputation i)

    {

        if (!i.FK_RubriqueBudgetaire.HasValue)

            throw new ArgumentException("La rubrique budgétaire est obligatoire pour une imputation AE.");



        if (string.IsNullOrWhiteSpace(i.LibelleItemAE))

            throw new ArgumentException("LibelleItemAE est obligatoire pour une imputation AE.");



        // Mois facultatif : ventilation temporelle, hors clé de contrôle annuel.

        if (i.Mois is < 1 or > 12)

            throw new ArgumentException("Le mois de ventilation AE doit être compris entre 1 et 12.");



        if (i.FK_ItemBI.HasValue)

            throw new ArgumentException("FK_ItemBI doit être absent pour une imputation AE.");



        if (!string.IsNullOrWhiteSpace(i.DetailBI))

            throw new ArgumentException("DetailBI doit être absent pour une imputation AE.");

    }



    private static void ValiderFormeBi(DemandePaiementImputation i)

    {

        if (i.FK_RubriqueBudgetaire.HasValue)

            throw new ArgumentException("FK_RubriqueBudgetaire doit être absent pour une imputation BI.");



        if (!string.IsNullOrWhiteSpace(i.LibelleItemAE))

            throw new ArgumentException("LibelleItemAE doit être absent pour une imputation BI.");



        if (i.FK_GroupeItemAE.HasValue)

            throw new ArgumentException("FK_GroupeItemAE doit être absent pour une imputation BI.");



        if (!i.FK_ItemBI.HasValue)

            throw new ArgumentException("FK_ItemBI est obligatoire pour une imputation BI.");



        if (string.IsNullOrWhiteSpace(i.DetailBI))

            throw new ArgumentException("Le détail BI est obligatoire pour une imputation BI.");



        // Mois facultatif : ventilation temporelle, hors clé de contrôle annuel.

        if (i.Mois is < 1 or > 12)

            throw new ArgumentException("Le mois de ventilation BI doit être compris entre 1 et 12.");

    }

}

