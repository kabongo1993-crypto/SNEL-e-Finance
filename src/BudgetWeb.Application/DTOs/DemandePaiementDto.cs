namespace BudgetWeb.Application.DTOs;

public record DemandePaiementListDto(
    long IdDemandePaiement,
    string Reference,
    DateOnly DateEmission,
    long IdExercice,
    short AnneeExercice,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long? IdDemandeur,
    string? CodeDemandeur,
    string? LibelleDemandeur,
    long? IdDepartement,
    string? CodeDepartement,
    string? LibelleDepartement,
    long IdCasDossier,
    string CodeCasDossier,
    string LibelleCasDossier,
    string Objet,
    decimal MontantBrut,
    string Devise,
    long? IdDevise,
    decimal? MontantUsd,
    long? IdTypeBudget,
    string? CodeTypeBudget,
    string? TypeBudgetSollicite,
    string? ItemSollicite,
    string Statut,
    DateTime? DateSoumission,
    DateTime DateCreation,
    long? IdUtilisateurAssigne = null,
    string? NomUtilisateurAssigne = null,
    string? PrenomUtilisateurAssigne = null,
    string? ModePaiementSollicite = null,
    string? TypeInstrumentPaiement = null);

public record DemandePaiementDetailDto(
    long IdDemandePaiement,
    string Reference,
    DateOnly DateEmission,
    string? LieuEmission,
    long IdExercice,
    short AnneeExercice,
    long? IdVersion,
    int? NumeroVersion,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long? IdDemandeur,
    string? CodeDemandeur,
    string? LibelleDemandeur,
    long? IdDepartement,
    string? CodeDepartement,
    string? LibelleDepartement,
    long IdCasDossier,
    string CodeCasDossier,
    string LibelleCasDossier,
    long? IdTypeBudget,
    string? CodeTypeBudget,
    string? TypeBudgetSollicite,
    string? ItemSollicite,
    string? DestinationSolliciteeAffichage,
    string Objet,
    string? CompteSection,
    decimal MontantBrut,
    string Devise,
    long? IdDevise,
    decimal? TauxConversion,
    decimal? MontantUsd,
    long? IdTauxChange,
    string? ModePaiementSollicite,
    string? TypeInstrumentPaiement,
    string? DevisePaiement,
    decimal? MontantPaiement,
    decimal? TauxPaiement,
    long? IdTauxChangePaiement,
    string Statut,
    string? MotifRetour,
    string? CommentaireRetour,
    DateTime DateCreation,
    DateTime? DateSoumission,
    DateTime? DateReception,
    DateTime? DateControle,
    DateTime? DateVisa,
    DateTime? DateRetour,
    IReadOnlyList<BeneficiaireDto> Beneficiaires,
    IReadOnlyList<DemandePaiementImputationDto> Imputations,
    IReadOnlyList<DemandePaiementPieceDto> Pieces,
    string? CircuitEntiteStatut = null,
    IReadOnlyList<ValidationEntiteDto>? ValidationsEntite = null,
    bool ModePaiementVerrouille = false,
    string? MotifVerrouillageModePaiement = null,
    long? IdUtilisateurAssigne = null,
    string? NomUtilisateurAssigne = null,
    string? PrenomUtilisateurAssigne = null);

public record CreateDemandePaiementRequest(
    DateOnly DateEmission,
    string? LieuEmission,
    long IdExercice,
    long IdDemandeur,
    /// <summary>Ignoré si fourni : l'UB est toujours celle du demandeur. Vérifiée si présente.</summary>
    long? IdUB,
    long IdCasDossier,
    string Objet,
    string? CompteSection,
    decimal MontantBrut,
    string Devise,
    string TypeBudgetSollicite,
    string? ItemSollicite,
    string ModePaiementSollicite,
    IReadOnlyList<CreateBeneficiaireRequest>? Beneficiaires = null,
    long? IdDevise = null);

public record UpdateDemandePaiementRequest(
    DateOnly DateEmission,
    string? LieuEmission,
    string Objet,
    string? CompteSection,
    decimal MontantBrut,
    string Devise,
    string TypeBudgetSollicite,
    string? ItemSollicite,
    string ModePaiementSollicite,
    IReadOnlyList<CreateBeneficiaireRequest>? Beneficiaires = null,
    long? IdDevise = null);

public record CreateBeneficiaireRequest(
    string TypeBeneficiaire,
    string NomComplet,
    string? Matricule,
    string? Fonction,
    string? RaisonSociale,
    string? Rccm,
    string? Adresse,
    string? Banque,
    string? NumeroCompte,
    bool EstPrincipal,
    int Ordre);

public record BeneficiaireDto(
    long IdBeneficiaire,
    string TypeBeneficiaire,
    string NomComplet,
    string? Matricule,
    string? Fonction,
    string? RaisonSociale,
    string? Rccm,
    string? Adresse,
    string? Banque,
    string? NumeroCompte,
    bool EstPrincipal,
    int Ordre);

public record CreateImputationRequest(
    int Ordre,
    long IdTypeBudget,
    long IdUB,
    long IdExercice,
    long? IdRubriqueBudgetaire,
    byte? Mois,
    string? LibelleItemAE,
    long? IdGroupeItemAE,
    long? IdItemBI,
    string? DetailBI,
    long? IdBudgetLigne,
    decimal MontantBrut,
    string Devise,
    int? NumeroFicheSuivi);

public record UpdateImputationRequest(
    int Ordre,
    long? IdRubriqueBudgetaire,
    byte? Mois,
    string? LibelleItemAE,
    long? IdGroupeItemAE,
    long? IdItemBI,
    string? DetailBI,
    long? IdBudgetLigne,
    decimal MontantBrut,
    string Devise,
    int? NumeroFicheSuivi);

public record DemandePaiementImputationDto(
    long IdImputation,
    int Ordre,
    long IdTypeBudget,
    string CodeTypeBudget,
    long IdUB,
    long IdExercice,
    long? IdRubriqueBudgetaire,
    byte? Mois,
    string? LibelleItemAE,
    long? IdGroupeItemAE,
    long? IdItemBI,
    string? DetailBI,
    long? IdBudgetLigne,
    decimal MontantBrut,
    string Devise,
    decimal TauxConversion,
    decimal MontantUsd,
    int? NumeroFicheSuivi);

public record UploadDemandePaiementPieceMetadata(
    long? IdPieceObligatoire,
    string CodeTypePiece,
    string Libelle);

/// <summary>Ancien DTO JSON (métadonnées seules) — conservé pour compatibilité de compilation des tests migrés.</summary>
[Obsolete("Utiliser UploadDemandePaiementPieceMetadata + flux fichier via AddPieceAsync.")]
public record CreateDemandePaiementPieceRequest(
    long? IdPieceObligatoire,
    string CodeTypePiece,
    string Libelle,
    bool EstObligatoire,
    string NomFichierOriginal,
    string CheminRelatif,
    string HashSha256,
    long TailleOctets);

public record DemandePaiementPieceDto(
    long IdPieceJointe,
    long? IdPieceObligatoire,
    string CodeTypePiece,
    string Libelle,
    bool EstObligatoire,
    string NomFichierOriginal,
    string CheminRelatif,
    string HashSha256,
    long TailleOctets,
    DateTime DateUpload);

public record DemandePaiementPieceContentDto(
    Stream Content,
    string FileName,
    string ContentType,
    long TailleOctets);

public record DemandePaiementQuery(
    long? IdExercice = null,
    long? IdUB = null,
    long? IdDepartement = null,
    long? IdCasDossier = null,
    long? IdTypeBudget = null,
    long? IdDemandeur = null,
    string? Statut = null,
    string? Reference = null,
    string? Beneficiaire = null,
    DateOnly? DateDebut = null,
    DateOnly? DateFin = null,
    string? Scope = null);

/// <summary>Scopes autorisés pour les compteurs DPM (alignés sur les pages liste).</summary>
public static class DemandePaiementListScope
{
    public const string MesDemandes = "mes-demandes";
    public const string ChargeDpm = "charge-dpm";
    public const string Budget = "budget";
    public const string JuniorDc = "junior-dc";
    public const string JuniorAe = "junior-ae";
    public const string JuniorBi = "junior-bi";
}

public record DemandePaiementCompteursQuery(
    string Scope,
    long? IdExercice = null,
    long? IdUB = null,
    long? IdDepartement = null,
    long? IdCasDossier = null,
    long? IdTypeBudget = null,
    long? IdDemandeur = null,
    string? Reference = null,
    string? Beneficiaire = null,
    DateOnly? DateDebut = null,
    DateOnly? DateFin = null)
{
    public DemandePaiementQuery ToListQuery()
        => new(
            IdExercice,
            IdUB,
            IdDepartement,
            IdCasDossier,
            IdTypeBudget,
            IdDemandeur,
            Statut: null,
            Reference,
            Beneficiaire,
            DateDebut,
            DateFin,
            Scope);
}

public record DemandePaiementCompteursDto(
    int Total,
    int Brouillon,
    int EnValidationN1,
    int EnValidationN2,
    int ValideeEntite,
    int Soumise,
    int EnTraitementDpm,
    int EnControleBudgetaire,
    int ACorriger,
    int ViseeBudgetairement);

/// <summary>Projection minimale pour compteurs (scoping métier en service).</summary>
public record DemandePaiementScopeRow(
    long IdDemandePaiement,
    string Statut,
    long FK_UniteBudgetaire,
    long? FK_UtilisateurCreation,
    long? FK_TypeBudget,
    string? CodeTypeBudget,
    long? FK_UtilisateurAssigne = null,
    long? FK_UtilisateurRetour = null);

public record DemandePaiementDetailCompletDto(
    DemandePaiementDetailDto Demande,
    IReadOnlyList<SnapshotDto> Snapshots,
    ControleBudgetaireDto? ControleBudgetaire,
    IReadOnlyList<PieceManquanteDto> PiecesManquantes,
    IReadOnlyList<JournalAuditDemandeDto> Historique,
    BilletConversionDto? BilletConversion,
    PieceCaisseDto? PieceCaisse = null,
    BonProvisoireDto? BonProvisoire = null,
    MinuteChequeDto? MinuteCheque = null);

public record LigneBudgetaireDisponibleQuery(
    long IdExercice,
    long IdUB,
    long IdTypeBudget,
    long? IdRubriqueBudgetaire,
    byte? Mois,
    string? LibelleItemAE,
    long? IdGroupeItemAE,
    long? IdItemBI,
    string? DetailBI);

public record LigneBudgetaireDisponibleDto(
    long? IdBudgetLigne,
    string CodeTypeBudget,
    decimal BudgetAnnuel,
    decimal? BudgetMensuel,
    decimal MontantPrevision,
    decimal CreditEngageAnnuel,
    decimal? CreditEngageMensuel,
    decimal CreditDisponibleAnnuel,
    decimal? CreditDisponibleMensuel,
    bool PrevisionExiste);

public record CasDossierDto(
    long IdCasDossier,
    string Code,
    string Libelle,
    int Ordre,
    bool Actif,
    IReadOnlyList<CasDossierPieceObligatoireDto> PiecesObligatoires);

public record CasDossierPieceObligatoireDto(
    long IdPieceObligatoire,
    string CodeTypePiece,
    string Libelle,
    int Ordre,
    bool Actif,
    bool Obligatoire);

public record CreateCasDossierRequest(
    string Code,
    string Libelle,
    int Ordre,
    bool Actif = true);

public record UpdateCasDossierRequest(
    string Libelle,
    int Ordre,
    bool Actif);

public record CreateCasDossierPieceRequest(
    string CodeTypePiece,
    string Libelle,
    int Ordre,
    bool Actif = true,
    bool Obligatoire = true);

public record UpdateCasDossierPieceRequest(
    string Libelle,
    int Ordre,
    bool Actif,
    bool Obligatoire);

public record DeviseDto(
    long IdDevise,
    string Code,
    string Libelle,
    string? Symbole,
    bool Actif);

public record CreateDeviseRequest(
    string Code,
    string Libelle,
    string? Symbole,
    bool Actif = true);

public record UpdateDeviseRequest(
    string Libelle,
    string? Symbole,
    bool Actif);

public record ParametreInstrumentPaiementDto(
    long? IdParametreInstrumentPaiement,
    string TypeInstrument,
    string? Sr,
    string? ComptabiliteGenerale,
    string? Cp,
    string? Cpa,
    string? CompteGeneral,
    string? CompteParticulier,
    string? CpCa,
    string? Ls,
    string? SuiviExtraComptable,
    decimal? MontantSuiviExtraComptable,
    string? NumeroAppariement,
    string? RecuInstitutionnel,
    bool Actif,
    bool EstConfigure);

public record UpsertParametreInstrumentRequest(
    string? Sr,
    string? ComptabiliteGenerale,
    string? Cp,
    string? Cpa,
    string? CompteGeneral,
    string? CompteParticulier,
    string? CpCa,
    string? Ls,
    string? SuiviExtraComptable,
    decimal? MontantSuiviExtraComptable,
    string? NumeroAppariement,
    string? RecuInstitutionnel,
    bool Actif = true);

public record PieceManquanteDto(
    long? IdPieceObligatoire,
    string CodeTypePiece,
    string Libelle);

public record ControleBudgetaireDto(
    bool EstValide,
    string? MotifRejet,
    IReadOnlyList<ControleImputationDto> Imputations);

public record ControleImputationDto(
    long IdImputation,
    int Ordre,
    string CodeTypeBudget,
    bool EstValide,
    string? MotifRejet,
    decimal BudgetAnnuel,
    decimal? BudgetMensuel,
    decimal CreditEngageAnnuel,
    decimal? CreditEngageMensuel,
    decimal EngagementEnCours,
    decimal CreditDisponibleAnnuel,
    decimal? CreditDisponibleMensuel,
    decimal MontantPrevision,
    decimal EcartPrevisionImputation,
    decimal MontantCourantUsd,
    long? IdBudgetLigne,
    bool DepassementMensuel = false,
    bool DepassementAnnuel = false);

public record GrilleImputationDcDto(
    long IdDemandePaiement,
    string Reference,
    string Devise,
    decimal MontantBrut,
    decimal? TauxConversion,
    decimal? MontantUsd,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    byte Mois,
    decimal BudgetAnnuelUb,
    decimal CreditEngageAnnuelUb,
    IReadOnlyList<LigneGrilleImputationDcDto> Lignes,
    decimal TotalRepartiBrut,
    decimal EcartBrut,
    decimal TotalRepartiUsd,
    decimal EcartUsd);

public record LigneGrilleImputationDcDto(
    long IdRubriqueBudgetaire,
    string CodeRubrique,
    string Libelle,
    bool EstCochee,
    long? IdImputation,
    long? IdBudgetLigne,
    bool PrevisionExiste,
    decimal BudgetMensuel,
    decimal CreditEngageMensuel,
    decimal EngagementEnCoursBrut,
    decimal EngagementEnCoursUsd,
    decimal MontantUsd,
    decimal CreditDisponibleMensuel,
    decimal BudgetAnnuel,
    decimal CreditEngageAnnuel,
    decimal CreditDisponibleAnnuel,
    decimal EngagementEnCoursAnnuelUsd = 0);

public record EnregistrerImputationsDcRequest(
    byte Mois,
    IReadOnlyList<LigneImputationDcRequest> Lignes);

public record LigneImputationDcRequest(
    long IdRubriqueBudgetaire,
    decimal MontantBrut);

public sealed record EngageDcMensuelCle(long IdRubriqueBudgetaire, byte Mois);

public sealed record EngageAeAnnuelCle(long IdRubriqueBudgetaire, string LibelleItemAE);

public record GrilleImputationAeDto(
    long IdDemandePaiement,
    string Reference,
    string Devise,
    decimal MontantBrut,
    decimal? TauxConversion,
    decimal? MontantUsd,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    string LibelleItemAE,
    long? IdGroupeItemAE,
    byte? Mois,
    decimal BudgetAnnuelItemUb,
    decimal CreditEngageAnnuelItemUb,
    IReadOnlyList<LigneGrilleImputationAeDto> Lignes,
    IReadOnlyList<LigneExistanteImputationAeDto> LignesExistantes,
    decimal TotalRepartiBrut,
    decimal EcartBrut,
    decimal TotalRepartiUsd,
    decimal EcartUsd);

public record LigneGrilleImputationAeDto(
    long IdRubriqueBudgetaire,
    string CodeRubrique,
    string Libelle,
    bool EstCochee,
    long? IdImputation,
    long? IdBudgetLigne,
    bool PrevisionExiste,
    decimal EngagementEnCoursBrut,
    decimal EngagementEnCoursUsd,
    decimal MontantUsd,
    decimal BudgetAnnuel,
    decimal CreditEngageAnnuel,
    decimal CreditDisponibleAnnuel,
    decimal EngagementEnCoursAnnuelUsd = 0);

/// <summary>Lignes AE déjà enregistrées sur la DPM (tous items / mois) — synthèse UI.</summary>
public record LigneExistanteImputationAeDto(
    long IdImputation,
    string LibelleItemAE,
    long? IdGroupeItemAE,
    byte? Mois,
    long IdRubriqueBudgetaire,
    string CodeRubrique,
    string LibelleRubrique,
    decimal MontantBrut,
    decimal MontantUsd);

public record EnregistrerImputationsAeRequest(
    string LibelleItemAE,
    long? IdGroupeItemAE,
    byte? Mois,
    IReadOnlyList<LigneImputationAeRequest> Lignes);

public record LigneImputationAeRequest(
    long IdRubriqueBudgetaire,
    decimal MontantBrut);

public sealed record EngageBiAnnuelCle(long IdItemBI, string DetailBI);

public record GrilleImputationBiDto(
    long IdDemandePaiement,
    string Reference,
    string Devise,
    decimal MontantBrut,
    decimal? TauxConversion,
    decimal? MontantUsd,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long IdItemBI,
    string CodeItemBI,
    string LibelleItemBI,
    byte? Mois,
    decimal BudgetAnnuelItemUb,
    decimal CreditEngageAnnuelItemUb,
    IReadOnlyList<LigneGrilleImputationBiDto> Lignes,
    IReadOnlyList<LigneExistanteImputationBiDto> LignesExistantes,
    decimal TotalRepartiBrut,
    decimal EcartBrut,
    decimal TotalRepartiUsd,
    decimal EcartUsd);

public record LigneGrilleImputationBiDto(
    string DetailBI,
    int Code,
    string Libelle,
    bool EstCochee,
    long? IdImputation,
    long? IdBudgetLigne,
    bool PrevisionExiste,
    decimal EngagementEnCoursBrut,
    decimal EngagementEnCoursUsd,
    decimal MontantUsd,
    decimal BudgetAnnuel,
    decimal CreditEngageAnnuel,
    decimal CreditDisponibleAnnuel,
    decimal EngagementEnCoursAnnuelUsd = 0);

public record LigneExistanteImputationBiDto(
    long IdImputation,
    long IdItemBI,
    string LibelleItemBI,
    string DetailBI,
    byte? Mois,
    decimal MontantBrut,
    decimal MontantUsd);

public record EnregistrerImputationsBiRequest(
    long IdItemBI,
    byte? Mois,
    IReadOnlyList<LigneImputationBiRequest> Lignes);

public record LigneImputationBiRequest(
    string DetailBI,
    decimal MontantBrut);

public record SnapshotDto(
    long IdSnapshot,
    long IdImputation,
    DateTime DateSnapshot,
    decimal? BudgetMensuel,
    decimal? CreditEngageMensuel,
    decimal? CreditDisponibleMensuelAvantVisa,
    decimal BudgetAnnuel,
    decimal CreditEngageAnnuel,
    decimal CreditDisponibleAnnuelAvantVisa,
    decimal MontantPrevision,
    decimal EcartPrevisionImputation,
    decimal MontantBrut,
    string Devise,
    decimal TauxConversion,
    decimal MontantUsd,
    long? IdBudgetLigne);

public record RetourDemandePaiementRequest(
    string MotifRetour,
    string? CommentaireRetour,
    string? EtapeConcernee = null);

public record RetenirSollicitationChargeRequest(
    string ModePaiementSollicite,
    string TypeBudgetSollicite,
    string? ItemSollicite = null);

public record TraitementChargeDpmRequest(
    string TypeInstrument,
    string? DevisePaiement,
    decimal? TauxPaiement,
    long? IdTauxChangePaiement,
    /// <summary>Retenu par le Chargé DP (remplace la sollicitation demandeur si fourni).</summary>
    string? ModePaiementSollicite = null,
    /// <summary>Retenu par le Chargé DP (remplace la sollicitation demandeur si fourni).</summary>
    string? TypeBudgetSollicite = null,
    string? ItemSollicite = null);

public record OrienterDemandePaiementRequest(long? IdUtilisateurCible = null);

public record HistoriqueDemandePaiementDto(
    long IdDemandePaiement,
    string Reference,
    IReadOnlyList<JournalAuditDemandeDto> Entrees);

public record DemandePaiementRoutageDto(
    long IdRoutage,
    string Action,
    string StatutSource,
    string StatutCible,
    long? IdUtilisateurSource,
    string? NomUtilisateurSource,
    string? PrenomUtilisateurSource,
    long? IdUtilisateurCible,
    string? NomUtilisateurCible,
    string? PrenomUtilisateurCible,
    DateTime DateRoutage,
    bool EstActif,
    string? Motif);

/// <summary>Destinataire d'un retour métier, projeté depuis le routage structuré (Lot 3.6.3).</summary>
public record DemandePaiementRetourDestinataireDto(
    long IdRoutage,
    string? TypeRetour,
    string ActionRoutage,
    string StatutSource,
    string StatutCible,
    long? IdUtilisateurDestinataire,
    string? NomUtilisateurDestinataire,
    string? PrenomUtilisateurDestinataire,
    DateTime DateRoutage,
    string? Motif);

public record JournalAuditDemandeDto(
    long IdAudit,
    string Operation,
    DateTime DateHeure,
    string? AnciennesValeurs,
    string? NouvellesValeurs);

public record ValidationEntiteRequest(string? Commentaire);

public record DeclarationValidationPhysiqueRequest(
    string NomSignataire,
    string? FonctionSignataire,
    DateOnly DateSignature,
    string? Commentaire);

public record ValidationEntiteDto(
    byte Niveau,
    byte Ordre,
    string Statut,
    string? ModeValidation,
    long? IdUtilisateurValidateur,
    string? NomUtilisateurValidateur,
    long? IdUtilisateurDeclarant,
    string? NomUtilisateurDeclarant,
    string? NomSignatairePhysique,
    string? FonctionSignatairePhysique,
    DateOnly? DateSignaturePhysique,
    DateTime? DateValidation,
    string? Commentaire);

public record DemandePaiementPieceDocumentDto(
    string Libelle,
    bool EstObligatoire,
    bool EstFournie,
    string? NomFichier);

public record DemandePaiementDocumentDto(
    long IdDemandePaiement,
    string Reference,
    DateOnly DateEmission,
    string? LieuEmission,
    string Objet,
    decimal MontantBrut,
    string Devise,
    string? DestinationSolliciteeAffichage,
    string? TypeBudgetSollicite,
    string? ItemSollicite,
    string? ModePaiementSollicite,
    string? CompteSection,
    string Statut,
    string StatutLibelle,
    string? LibelleDemandeur,
    string? LibelleCasDossier,
    DateTime? DateSoumission,
    DateTime DateImpression,
    string IdentifiantVerification,
    IReadOnlyList<BeneficiaireDto> Beneficiaires,
    IReadOnlyList<ValidationEntiteDto> ValidationsEntite,
    IReadOnlyList<DemandePaiementPieceDocumentDto> PiecesJustificatives,
    bool DocumentSignePhysiquePresent);

/* ========== Billet de conversion ========== */

public record BilletConversionDto(
    long IdBilletConversion,
    long IdDemandePaiement,
    string Statut,
    DateOnly DateConversion,
    string DeviseOrigine,
    decimal MontantDeviseOrigine,
    decimal TauxApplique,
    decimal MontantCdf,
    long? IdTauxChange,
    string? DemandeChequeNumero,
    string? CoursEchangeBanque,
    decimal? SoldeAPayerDevise,
    long IdUtilisateurEtabli,
    string? NomUtilisateurEtabli,
    DateTime DateEtabli,
    string? NomUtilisateurApprouve,
    string? NomUtilisateurVisa,
    /// <summary>Données affichage — provenant de la demande (non dupliquées en base).</summary>
    string ReferenceDemande,
    string BeneficiaireAffichage);

public record EtablirBilletConversionRequest(
    DateOnly? DateConversion = null,
    string? DemandeChequeNumero = null,
    string? CoursEchangeBanque = null,
    decimal? SoldeAPayerDevise = null);

public record BilletConversionDocumentDto(
    long IdDemandePaiement,
    string Reference,
    string Beneficiaire,
    decimal MontantDeviseOrigine,
    string DeviseOrigine,
    decimal TauxApplique,
    DateOnly DateConversion,
    string? DemandeChequeNumero,
    string? CoursEchangeBanque,
    decimal MontantCdf,
    decimal? SoldeAPayerDevise,
    string? EtabliPar,
    string? ApprouvePar,
    string? Visa,
    DateTime DateImpression);

/* ========== Documents instrument de paiement ========== */

public record PieceCaisseDto(
    long IdPieceCaisse,
    long IdDemandePaiement,
    string Statut,
    string NumeroPiece,
    DateOnly DatePiece,
    decimal MontantFc,
    string MontantEnLettres,
    string ReferenceDemande,
    string Motif,
    string? PieceJustificative,
    string BeneficiaireAffichage,
    string? BeneficiaireMatricule,
    string? BeneficiaireIdentite,
    string? RecuSnel,
    string? Sr,
    string? ComptabiliteGenerale,
    string? Cp,
    string? Cpa,
    string? NumeroAppariement,
    string IdentifiantVerification,
    long IdUtilisateurEtabli,
    string? NomUtilisateurEtabli,
    DateTime DateEtabli);

public record EtablirPieceCaisseRequest(DateOnly? DatePiece = null);

public record PieceCaisseDocumentDto(
    string TitreDocument,
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
    string? EtabliPar,
    DateTime DateImpression);

public record BonProvisoireDto(
    long IdBonProvisoire,
    long IdDemandePaiement,
    string Statut,
    string NumeroBon,
    DateOnly DateBon,
    decimal MontantFc,
    string MontantEnLettres,
    string ReferenceDemande,
    string Motif,
    string? MentionJustificationRetrait,
    string BeneficiaireAffichage,
    string? BeneficiaireMatricule,
    string? BeneficiaireIdentite,
    string? DirectionBeneficiaire,
    string? RecuCaisseCentrale,
    string? CompteGeneral,
    string? CompteParticulier,
    string? NumeroAppariement,
    string IdentifiantVerification,
    long IdUtilisateurEtabli,
    string? NomUtilisateurEtabli,
    DateTime DateEtabli);

public record EtablirBonProvisoireRequest(DateOnly? DateBon = null);

public record BonProvisoireDocumentDto(
    string TitreDocument,
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
    string? EtabliPar,
    DateTime DateImpression);

public record MinuteChequeDto(
    long IdMinuteCheque,
    long IdDemandePaiement,
    string Statut,
    string NumeroOp,
    DateOnly DateDocument,
    decimal MontantPaiement,
    string DevisePaiement,
    string MontantEnLettres,
    string ReferenceDemande,
    string Motif,
    string BeneficiaireAffichage,
    string? BeneficiaireAdresse,
    string? BeneficiaireBanque,
    string? BeneficiaireNumeroCompte,
    string? CompteGeneral,
    string? CpCa,
    string? Ls,
    string? SuiviExtraComptable,
    string? NumeroAppariement,
    decimal? MontantSuiviExtraComptable,
    string IdentifiantVerification,
    long IdUtilisateurEtabli,
    string? NomUtilisateurEtabli,
    DateTime DateEtabli);

public record EtablirMinuteChequeRequest(DateOnly? DateDocument = null);

public record MinuteChequeDocumentDto(
    string TitreDocument,
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
    string? EtabliPar,
    DateTime DateImpression);

/* ========== Demandeur (référentiel DPM) ========== */

public record DemandeurDto(
    long IdDemandeur,
    string Code,
    string Libelle,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateModification);

public record CreateDemandeurRequest(
    string Code,
    string Libelle,
    long IdUB);

public record UpdateDemandeurRequest(
    string Code,
    string Libelle,
    long IdUB,
    bool Actif);
