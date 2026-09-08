namespace BudgetWeb.Application.DTOs;

public record RepartitionMensuelleDto(byte Mois, decimal Montant);

public record PrevisionBudgetaireDto(
    long IdPrevision,
    long IdVersion,
    int NumeroVersion,
    string LibelleVersion,
    string StatutVersion,
    short AnneeExercice,
    long IdTypeBudget,
    string CodeType,
    string LibelleType,
    long IdModePrevision,
    string CodeMode,
    string LibelleMode,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long? IdRB,
    string? CodeRB,
    string? LibelleRB,
    long? IdItemBI,
    string? CodeItemBI,
    string? LibelleItemBI,
    long? IdGroupeItemAE,
    string? LibelleGroupeItemAE,
    string? LibelleItemAE,
    string? DetailBI,
    decimal MontantAnnuel,
    IReadOnlyList<RepartitionMensuelleDto> Repartitions,
    DateTime DateCreation,
    long IdUtilisateurCreation,
    DateTime? DateModification,
    long? IdUtilisateurModification);

public record CreatePrevisionBudgetaireRequest(
    long IdVersion,
    long IdTypeBudget,
    long IdModePrevision,
    long IdUB,
    long? IdRB,
    long? IdItemBI,
    long? IdGroupeItemAE,
    string? LibelleItemAE,
    string? DetailBI,
    decimal? MontantAnnuel,
    IReadOnlyList<RepartitionMensuelleDto>? Repartitions,
    long IdUtilisateurCreation);

public record UpdatePrevisionBudgetaireRequest(
    long? IdGroupeItemAE,
    string? LibelleItemAE,
    string? DetailBI,
    decimal? MontantAnnuel,
    IReadOnlyList<RepartitionMensuelleDto>? Repartitions,
    long IdUtilisateurModification);

public record PrevisionLigneSauvegardeDto(
    long? IdPrevision,
    long? IdRB,
    string? DetailBI,
    decimal? MontantAnnuel,
    bool Supprimer,
    IReadOnlyList<RepartitionMensuelleDto>? Repartitions);

public record SauvegarderGrillePrevisionRequest(
    long IdVersion,
    long IdTypeBudget,
    long IdModePrevision,
    long IdUB,
    long? IdItemBI,
    long? IdGroupeItemAE,
    string? LibelleItemAE,
    long IdUtilisateur,
    IReadOnlyList<PrevisionLigneSauvegardeDto> Lignes);

public record SauvegarderGrillePrevisionResultDto(
    int LignesCreees,
    int LignesModifiees,
    int LignesSupprimees,
    IReadOnlyList<PrevisionBudgetaireDto> Previsions);

public record PrevisionGrilleLigneDto(
    long? IdPrevision,
    long? IdRB,
    string? CodeRB,
    string? LibelleRB,
    long? ParentIdRB,
    int? NiveauRB,
    bool EstSection,
    string? DetailBI,
    decimal MontantAnnuel,
    decimal CumulMensuel,
    IReadOnlyList<RepartitionMensuelleDto> Repartitions,
    long? IdGroupeRB = null,
    string? CodeGroupe = null,
    string? LibelleGroupe = null,
    int? OrdreAffichageGroupe = null);

/// <summary>
/// Projection minimale pour construire la grille (pas de graphe Version/UB/Utilisateur).
/// </summary>
public record PrevisionGrilleSourceDto(
    long IdPrevision,
    long? IdRB,
    string? DetailBI,
    string? LibelleItemAE,
    decimal MontantAnnuel,
    IReadOnlyList<RepartitionMensuelleDto> Repartitions);

public record PrevisionGrilleDto(
    long IdVersion,
    string StatutVersion,
    bool ModificationAutorisee,
    long IdTypeBudget,
    string CodeType,
    long IdModePrevision,
    string CodeMode,
    long IdUB,
    string CodeUB,
    long? IdItemBI,
    string? LibelleItemAE,
    long? IdGroupeItemAE,
    IReadOnlyList<PrevisionGrilleLigneDto> Lignes);

/// <summary>Grille paginée (DC/AE) — même logique métier, chargement progressif.</summary>
public record PrevisionGrillePageDto(
    long IdVersion,
    string StatutVersion,
    bool ModificationAutorisee,
    long IdTypeBudget,
    string CodeType,
    long IdModePrevision,
    string CodeMode,
    long IdUB,
    string CodeUB,
    long? IdItemBI,
    string? LibelleItemAE,
    long? IdGroupeItemAE,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasNextPage,
    IReadOnlyList<PrevisionGrilleLigneDto> Lignes);

public record PrevisionResumeCategorieDto(
    string CodeType,
    string LibelleType,
    decimal MontantTotal,
    int NombreLignes);

public record GroupeItemAEDto(
    long IdGroupeItemAE,
    string Libelle,
    bool Actif,
    DateTime DateCreation,
    int NombrePrevisions);

public record CreateGroupeItemAERequest(string Libelle, bool? Actif);

public record UpdateGroupeItemAERequest(string Libelle, bool? Actif);
