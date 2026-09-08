namespace BudgetWeb.Application.DTOs;

public record VersionBudgetaireDto(
    long IdVersion,
    long IdExercice,
    short AnneeExercice,
    string StatutExercice,
    int NumeroVersion,
    string Libelle,
    long? IdVersionPrecedente,
    int? NumeroVersionPrecedente,
    string? LibelleVersionPrecedente,
    DateTime DateCreation,
    DateOnly DateDebutEffet,
    DateOnly? DateFinEffet,
    string? Motif,
    string Statut,
    long IdUtilisateurCreation,
    string NomUtilisateurCreation,
    long? IdUtilisateurValidation,
    string? NomUtilisateurValidation,
    DateTime? DateValidation,
    long? IdUtilisateurSoumission,
    string? NomUtilisateurSoumission,
    DateTime? DateSoumission,
    long? IdUtilisateurControle,
    string? NomUtilisateurControle,
    DateTime? DateControle,
    long? IdUtilisateurRejet,
    string? NomUtilisateurRejet,
    DateTime? DateRejet,
    string? MotifRejet,
    int NombrePrevisions,
    int NombreTransferts,
    int NombreVersionsSuivantes);

/// <summary>Création : le statut est toujours forcé à BROUILLON côté service.</summary>
public record CreateVersionBudgetaireRequest(
    long IdExercice,
    int? NumeroVersion,
    string Libelle,
    long? IdVersionPrecedente,
    DateOnly? DateDebutEffet,
    DateOnly? DateFinEffet,
    string? Motif,
    string? Statut,
    long IdUtilisateurCreation,
    long? IdUtilisateurValidation,
    DateOnly? DateValidation);

/// <summary>Mise à jour métadonnées — le statut ne peut pas être changé via ce DTO.</summary>
public record UpdateVersionBudgetaireRequest(
    long IdExercice,
    int? NumeroVersion,
    string Libelle,
    long? IdVersionPrecedente,
    DateOnly? DateDebutEffet,
    DateOnly? DateFinEffet,
    string? Motif,
    long IdUtilisateurCreation);

public record RejeterVersionRequest(string Motif);

public record UtilisateurLookupDto(
    long IdUtilisateur,
    string NomUtilisateur,
    string Nom,
    string? Prenom,
    bool Actif);
