using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Création de demandes en brouillon avec invariants de base (demande initiale Département).</summary>
public static class DemandePaiementFactory
{
    /// <summary>
    /// Brouillon initial : Demandeur + UB + montant sollicité + devise
    /// + destination budgétaire sollicitée + mode paiement sollicité.
    /// Sans FK_TypeBudget, taux ni montant converti.
    /// </summary>
    public static Entities.DemandePaiement CreerBrouillon(
        string reference,
        DateOnly dateEmission,
        long fkExerciceBudgetaire,
        long fkUniteBudgetaire,
        long fkDemandeur,
        long fkCasDossier,
        string objet,
        decimal montantSollicite,
        string deviseSollicitee,
        string typeBudgetSollicite,
        string? itemSollicite,
        string modePaiementSollicite,
        long fkUtilisateurCreation,
        DateTime dateCreationUtc,
        string? lieuEmission = null,
        long? fkVersionBudgetaire = null,
        string? compteSection = null,
        long? fkUbProposeeParClient = null,
        long? fkDevise = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("La référence est obligatoire.", nameof(reference));

        if (string.IsNullOrWhiteSpace(objet))
            throw new ArgumentException("L'objet est obligatoire.", nameof(objet));

        if (fkDemandeur <= 0)
            throw new ArgumentException("Le demandeur est obligatoire.", nameof(fkDemandeur));

        if (fkUniteBudgetaire <= 0)
            throw new ArgumentException("L'unité budgétaire est obligatoire.", nameof(fkUniteBudgetaire));

        if (fkCasDossier <= 0)
            throw new ArgumentException("Le cas de dossier est obligatoire.", nameof(fkCasDossier));

        if (montantSollicite < 0)
            throw new ArgumentException("Le montant sollicité ne peut pas être négatif.", nameof(montantSollicite));

        DemandeurRules.ExigerCoherenceUbDemandeur(fkUniteBudgetaire, fkUbProposeeParClient);
        var codeDevise = DemandePaiementMontants.NormaliserCodeDevise(deviseSollicitee);
        DemandePaiementMontants.ValiderDevise(codeDevise);
        DestinationBudgetaireSolliciteeRules.Valider(typeBudgetSollicite, itemSollicite);

        var mode = ModePaiementDpm.Normaliser(modePaiementSollicite);
        if (!ModePaiementDpm.IsValid(mode))
            throw new ArgumentException("Le mode de paiement sollicité doit être CAISSE ou BANQUE.");

        return new Entities.DemandePaiement
        {
            Reference = reference.Trim(),
            DateEmission = dateEmission,
            LieuEmission = lieuEmission?.Trim(),
            FK_ExerciceBudgetaire = fkExerciceBudgetaire,
            FK_VersionBudgetaire = fkVersionBudgetaire,
            FK_UniteBudgetaire = fkUniteBudgetaire,
            FK_Demandeur = fkDemandeur,
            FK_CasDossier = fkCasDossier,
            TypeBudgetSollicite = DestinationBudgetaireSolliciteeRules.NormaliserType(typeBudgetSollicite),
            ItemSollicite = DestinationBudgetaireSolliciteeRules.NormaliserItem(itemSollicite),
            FK_TypeBudget = null,
            Objet = objet.Trim(),
            CompteSection = compteSection?.Trim(),
            MontantBrut = montantSollicite,
            FK_Devise = fkDevise,
            Devise = codeDevise,
            TauxConversion = null,
            MontantUsd = null,
            FK_TauxChange = null,
            ModePaiementSollicite = mode,
            Statut = StatutDemandePaiement.Brouillon,
            FK_UtilisateurCreation = fkUtilisateurCreation,
            DateCreation = dateCreationUtc
        };
    }

    /// <summary>Interdit la création directe en statut autre que BROUILLON.</summary>
    public static void VerifierStatutInitial(string? statut)
    {
        var normalise = StatutDemandePaiement.Normaliser(statut);
        if (!string.Equals(normalise, StatutDemandePaiement.Brouillon, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Une demande de paiement ne peut être créée qu'en statut BROUILLON.");
    }
}
