namespace BudgetWeb.Application.DTOs;

/// <summary>Compteurs par statut (distinct Version×UB).</summary>
public record SuiviPrevisionCompteursDto(
    int Brouillons,
    int Soumises,
    int Controlees,
    int Validees,
    int Rejetees);

/// <summary>Ligne synthèse Version × UB (totaux DC/AE/BI calculés, non persistés).</summary>
public record SuiviPrevisionUbResumeDto(
    long IdVersion,
    int NumeroVersion,
    string LibelleVersion,
    string Statut,
    long IdExercice,
    int Annee,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    decimal MontantTotal,
    DateTime DateDerniereModification,
    DateTime? DateSoumission,
    string? NomUtilisateurSoumission,
    DateTime? DateRejet,
    string? NomUtilisateurRejet,
    string? MotifRejet);

public record SuiviPrevisionListeDto(
    SuiviPrevisionCompteursDto Compteurs,
    IReadOnlyList<SuiviPrevisionUbResumeDto> Lignes);

/// <summary>En-tête + totaux pour une Version×UB (sans charger toutes les RB).</summary>
public record SuiviUbDetailDto(
    long IdVersion,
    int NumeroVersion,
    string LibelleVersion,
    string Statut,
    long IdExercice,
    int Annee,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    decimal MontantTotal,
    DateTime? DateSoumission,
    string? NomUtilisateurSoumission,
    DateTime? DateControle,
    string? NomUtilisateurControle,
    DateTime? DateValidation,
    string? NomUtilisateurValidation,
    DateTime? DateRejet,
    string? NomUtilisateurRejet,
    string? MotifRejet,
    bool PeutControler,
    bool PeutValider,
    bool PeutRejeter);

public record SuiviUbLigneDcDto(
    bool EstSection,
    long? IdGroupeRB,
    string? CodeGroupe,
    string? LibelleGroupe,
    int? OrdreAffichageGroupe,
    long? IdRB,
    string? CodeRB,
    string Libelle,
    decimal MontantAnnuel,
    bool RepartitionDefinie,
    IReadOnlyList<decimal>? MontantsMensuels);

public record SuiviUbLigneAeDto(
    string LibelleItemAE,
    string? LibelleGroupeAE,
    long? IdRB,
    string? CodeRB,
    string? LibelleRB,
    decimal MontantAnnuel,
    string CodeMode,
    bool RepartitionDefinie,
    IReadOnlyList<decimal>? MontantsMensuels);

public record SuiviUbLigneBiDto(
    long IdItemBI,
    string CodeItem,
    string LibelleItem,
    string? DetailBI,
    decimal MontantAnnuel,
    string CodeMode,
    bool RepartitionDefinie,
    IReadOnlyList<decimal>? MontantsMensuels);

public record SuiviUbDetailLignesDto(
    string CodeType,
    IReadOnlyList<SuiviUbLigneDcDto>? LignesDC,
    IReadOnlyList<SuiviUbLigneAeDto>? LignesAE,
    IReadOnlyList<SuiviUbLigneBiDto>? LignesBI);
