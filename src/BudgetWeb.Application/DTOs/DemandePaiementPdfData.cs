namespace BudgetWeb.Application.DTOs;

/// <summary>Projection minimale pour contrôle d'accès et gardes métier (pipelines PDF).</summary>
public sealed record DemandePaiementAccesContext(
    long IdDemandePaiement,
    string Statut,
    long FK_UniteBudgetaire,
    long FK_UtilisateurCreation,
    long? FK_UtilisateurAssigne,
    string? TypeBudgetSollicite,
    string? CodeTypeBudget,
    string ModePaiementSollicite,
    string Devise);

/// <summary>Nom affichable utilisateur — projection PDF sans entité complète.</summary>
public sealed record PdfUserDisplayName(
    string? Nom,
    string? Prenom,
    string? NomUtilisateur);

/// <summary>Bénéficiaire principal — projection pour billet de conversion PDF.</summary>
public sealed record PdfBeneficiairePrincipal(
    string? RaisonSociale,
    string NomComplet,
    bool EstPrincipal,
    int Ordre);

/// <summary>Données imprimables pièce caisse — projection dpm.PIECE_CAISSE.</summary>
public sealed record PieceCaissePdfData(
    string NumeroPiece,
    DateOnly DatePiece,
    string ReferenceDemande,
    string Motif,
    string? PieceJustificative,
    string BeneficiaireAffichage,
    string? BeneficiaireMatricule,
    string? BeneficiaireIdentite,
    decimal MontantFc,
    string MontantEnLettres,
    string? RecuSnel,
    string? Sr,
    string? ComptabiliteGenerale,
    string? Cp,
    string? Cpa,
    string? NumeroAppariement,
    string IdentifiantVerification,
    PdfUserDisplayName? UtilisateurEtabli);

/// <summary>Données imprimables bon provisoire — projection dpm.BON_PROVISOIRE.</summary>
public sealed record BonProvisoirePdfData(
    string NumeroBon,
    DateOnly DateBon,
    string ReferenceDemande,
    string Motif,
    string? MentionJustificationRetrait,
    string BeneficiaireAffichage,
    string? BeneficiaireMatricule,
    string? BeneficiaireIdentite,
    string? DirectionBeneficiaire,
    decimal MontantFc,
    string MontantEnLettres,
    string? RecuCaisseCentrale,
    string? CompteGeneral,
    string? CompteParticulier,
    string? NumeroAppariement,
    string IdentifiantVerification,
    PdfUserDisplayName? UtilisateurEtabli);

/// <summary>Données imprimables minute chèque — projection dpm.MINUTE_CHEQUE.</summary>
public sealed record MinuteChequePdfData(
    string NumeroOp,
    DateOnly DateDocument,
    string ReferenceDemande,
    string Motif,
    string BeneficiaireAffichage,
    string? BeneficiaireAdresse,
    string? BeneficiaireBanque,
    string? BeneficiaireNumeroCompte,
    decimal MontantPaiement,
    string DevisePaiement,
    string MontantEnLettres,
    string? CompteGeneral,
    string? CpCa,
    string? Ls,
    string? SuiviExtraComptable,
    string? NumeroAppariement,
    decimal? MontantSuiviExtraComptable,
    string IdentifiantVerification,
    PdfUserDisplayName? UtilisateurEtabli);

/// <summary>Données imprimables billet de conversion — projection dpm.BILLET_CONVERSION.</summary>
public sealed record BilletConversionPdfData(
    long IdDemandePaiement,
    string ReferenceDemande,
    PdfBeneficiairePrincipal? BeneficiairePrincipal,
    decimal MontantDeviseOrigine,
    string DeviseOrigine,
    decimal TauxApplique,
    DateOnly DateConversion,
    string? DemandeChequeNumero,
    string? CoursEchangeBanque,
    decimal MontantCdf,
    decimal? SoldeAPayerDevise,
    PdfUserDisplayName? UtilisateurEtabli,
    PdfUserDisplayName? UtilisateurApprouve,
    PdfUserDisplayName? UtilisateurVisa);

public sealed record DemandePaiementPdfBeneficiaireRow(
    string TypeBeneficiaire,
    string NomComplet,
    string? Matricule,
    string? Fonction,
    string? RaisonSociale,
    string? Rccm,
    string? Banque,
    string? NumeroCompte,
    bool EstPrincipal,
    int Ordre);

public sealed record DemandePaiementPdfValidationRow(
    byte Niveau,
    byte Ordre,
    string Statut,
    string? ModeValidation,
    string? NomUtilisateurValidateur,
    string? NomUtilisateurDeclarant,
    string? NomSignatairePhysique,
    string? FonctionSignatairePhysique,
    DateOnly? DateSignaturePhysique,
    DateTime? DateValidation,
    string? Commentaire);

/// <summary>Données imprimables DPM — projections séparées header / bénéficiaires / validations.</summary>
public sealed record DemandePaiementPdfData(
    long IdDemandePaiement,
    string Reference,
    DateOnly DateEmission,
    string? LieuEmission,
    string Objet,
    decimal MontantBrut,
    string Devise,
    string? TypeBudgetSollicite,
    string? ItemSollicite,
    string? ModePaiementSollicite,
    string? CompteSection,
    string Statut,
    DateTime? DateSoumission,
    long FK_Demandeur,
    long FK_CasDossier,
    string? LibelleDemandeur,
    string? LibelleCasDossier,
    bool DocumentSignePhysiquePresent,
    IReadOnlyList<DemandePaiementPdfBeneficiaireRow> Beneficiaires,
    IReadOnlyList<DemandePaiementPdfValidationRow> Validations);

public static class PdfUserDisplayNameFormatting
{
    public static string? Format(PdfUserDisplayName? user)
    {
        if (user is null)
            return null;

        var parts = new[] { user.Prenom, user.Nom }.Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(joined) ? user.NomUtilisateur : joined;
    }
}

public static class PdfBeneficiairePrincipalFormatting
{
    public static string ResoudreAffichage(PdfBeneficiairePrincipal? principal)
    {
        if (principal is null)
            return "—";

        if (!string.IsNullOrWhiteSpace(principal.RaisonSociale))
            return principal.RaisonSociale.Trim();

        return principal.NomComplet.Trim();
    }
}
