using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

public static class ValidationEntiteRules
{
    public static DemandePaiementValidation ObtenirNiveau(
        Entities.DemandePaiement demande,
        byte niveau)
    {
        ArgumentNullException.ThrowIfNull(demande);
        if (!ValidationEntiteNiveau.IsValid(niveau))
            throw new ArgumentException("Niveau de validation invalide.", nameof(niveau));

        var row = demande.ValidationsEntite.FirstOrDefault(v => v.Niveau == niveau);
        if (row is null)
            throw new InvalidOperationException($"Validation niveau {niveau} introuvable pour cette demande.");

        return row;
    }

    public static void ExigerNiveauEnAttente(DemandePaiementValidation validation, byte niveauAttendu)
    {
        if (validation.Niveau != niveauAttendu)
            throw new InvalidOperationException($"Niveau de validation attendu : {niveauAttendu}.");

        if (StatutValidationEntite.Normaliser(validation.Statut) != StatutValidationEntite.EnAttente)
            throw new InvalidOperationException("Cette validation n'est plus en attente.");
    }

    public static void ExigerN1Validee(Entities.DemandePaiement demande)
    {
        var n1 = ObtenirNiveau(demande, ValidationEntiteNiveau.N1);
        if (StatutValidationEntite.Normaliser(n1.Statut) != StatutValidationEntite.Validee)
            throw new InvalidOperationException("La validation niveau 1 doit être satisfaite avant le niveau 2.");
    }

    public static void ExigerEmpreinteCoherente(
        Entities.DemandePaiement demande,
        DemandePaiementValidation validation)
    {
        var empreinte = DemandePaiementEmpreinte.Calculer(demande);
        if (!string.IsNullOrEmpty(validation.EmpreinteDonnees)
            && !string.Equals(validation.EmpreinteDonnees, empreinte, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Les données de la demande ont été modifiées depuis la validation. Reprenez le circuit entité.");
    }

    public static void ExigerEntiteEntierementValidee(Entities.DemandePaiement demande)
    {
        foreach (var niveau in ValidationEntiteNiveau.Valeurs)
        {
            var v = ObtenirNiveau(demande, niveau);
            ExigerEmpreinteCoherente(demande, v);
            if (StatutValidationEntite.Normaliser(v.Statut) != StatutValidationEntite.Validee)
                throw new InvalidOperationException("Les validations entité N1 et N2 doivent être satisfaites.");
        }
    }

    public static bool PossedeValidationPhysique(Entities.DemandePaiement demande)
        => demande.ValidationsEntite.Any(v =>
            StatutValidationEntite.Normaliser(v.Statut) == StatutValidationEntite.Validee
            && ModeValidationEntite.Normaliser(v.ModeValidation) == ModeValidationEntite.Physique);

    public static void ExigerDocumentSigneSiPhysique(Entities.DemandePaiement demande)
    {
        if (!PossedeValidationPhysique(demande))
            return;

        var possede = demande.PiecesJointes.Any(p =>
            string.Equals(p.CodeTypePiece, TypePieceJointeDpm.DocumentDpmSigne, StringComparison.OrdinalIgnoreCase));

        if (!possede)
            throw new InvalidOperationException(
                "Le document DPM signé physiquement (DOCUMENT_DPM_SIGNE) est obligatoire avant validation entité complète.");
    }

    public static void ValiderDeclarationPhysique(
        string? nomSignataire,
        DateOnly? dateSignature)
    {
        if (string.IsNullOrWhiteSpace(nomSignataire))
            throw new ArgumentException("Le nom du signataire physique est obligatoire.");

        if (dateSignature is null)
            throw new ArgumentException("La date de signature physique est obligatoire.");
    }

    public static void ReinitialiserValidations(Entities.DemandePaiement demande)
    {
        foreach (var v in demande.ValidationsEntite)
            ReinitialiserValidation(v);
    }

    public static void ReinitialiserValidationN2(Entities.DemandePaiement demande)
    {
        var n2 = ObtenirNiveau(demande, ValidationEntiteNiveau.N2);
        ReinitialiserValidation(n2);
    }

    public static void ReinitialiserValidationN1(Entities.DemandePaiement demande)
    {
        var n1 = ObtenirNiveau(demande, ValidationEntiteNiveau.N1);
        ReinitialiserValidation(n1);
    }

    public static bool EstValidee(DemandePaiementValidation validation)
        => StatutValidationEntite.Normaliser(validation.Statut) == StatutValidationEntite.Validee;

    public static bool EstEnAttente(DemandePaiementValidation validation)
        => StatutValidationEntite.Normaliser(validation.Statut) == StatutValidationEntite.EnAttente;

    public static bool EstValideePhysique(DemandePaiementValidation validation)
        => EstValidee(validation)
           && ModeValidationEntite.Normaliser(validation.ModeValidation) == ModeValidationEntite.Physique;

    public static bool EstValideeElectronique(DemandePaiementValidation validation)
        => EstValidee(validation)
           && ModeValidationEntite.Normaliser(validation.ModeValidation) == ModeValidationEntite.Electronique;

    public static bool EstActeurValidation(DemandePaiementValidation validation, long userId)
        => validation.FK_UtilisateurValidateur == userId
           || validation.FK_UtilisateurDeclarant == userId;

    public static void ExigerValidationValidee(DemandePaiementValidation validation, byte niveau)
    {
        if (validation.Niveau != niveau)
            throw new InvalidOperationException($"Niveau de validation attendu : {niveau}.");

        if (!EstValidee(validation))
            throw new InvalidOperationException("Cette validation n'est pas enregistrée comme validée.");
    }

    /// <summary>
    /// Statut cible après annulation de la validation N2 (physique : cascade possible vers brouillon).
    /// </summary>
    public static string ResoudreStatutApresAnnulationN2(Entities.DemandePaiement demande)
    {
        var n1 = ObtenirNiveau(demande, ValidationEntiteNiveau.N1);
        var n2 = ObtenirNiveau(demande, ValidationEntiteNiveau.N2);

        ExigerValidationValidee(n2, ValidationEntiteNiveau.N2);

        if (EstValideePhysique(n2) && EstValideePhysique(n1))
            return StatutDemandePaiement.Brouillon;

        return StatutDemandePaiement.EnValidationN1;
    }

    private static void ReinitialiserValidation(DemandePaiementValidation validation)
    {
        validation.Statut = StatutValidationEntite.EnAttente;
        validation.ModeValidation = null;
        validation.FK_UtilisateurValidateur = null;
        validation.FK_UtilisateurDeclarant = null;
        validation.NomSignatairePhysique = null;
        validation.FonctionSignatairePhysique = null;
        validation.DateSignaturePhysique = null;
        validation.DateValidation = null;
        validation.Commentaire = null;
        validation.EmpreinteDonnees = null;
    }

    /// <summary>
    /// Réinitialise N1/N2 si l'empreinte métier courante diffère de celle enregistrée à la validation.
    /// </summary>
    public static void InvaliderValidationsSiEmpreinteObsolete(Entities.DemandePaiement demande)
    {
        ArgumentNullException.ThrowIfNull(demande);

        if (demande.ValidationsEntite.Count == 0)
            return;

        var anyValidated = demande.ValidationsEntite.Any(v =>
            StatutValidationEntite.Normaliser(v.Statut) == StatutValidationEntite.Validee);

        if (!anyValidated)
            return;

        var empreinte = DemandePaiementEmpreinte.Calculer(demande);
        var stale = demande.ValidationsEntite.Any(v =>
            !string.IsNullOrEmpty(v.EmpreinteDonnees)
            && !string.Equals(v.EmpreinteDonnees, empreinte, StringComparison.OrdinalIgnoreCase));

        if (stale)
            ReinitialiserValidations(demande);
    }

    public static IReadOnlyList<DemandePaiementValidation> CreerValidationsInitiales(long idDemande)
        =>
        [
            new DemandePaiementValidation
            {
                FK_DemandePaiement = idDemande,
                Niveau = ValidationEntiteNiveau.N1,
                Ordre = ValidationEntiteNiveau.N1,
                Statut = StatutValidationEntite.EnAttente,
            },
            new DemandePaiementValidation
            {
                FK_DemandePaiement = idDemande,
                Niveau = ValidationEntiteNiveau.N2,
                Ordre = ValidationEntiteNiveau.N2,
                Statut = StatutValidationEntite.EnAttente,
            },
        ];
}
