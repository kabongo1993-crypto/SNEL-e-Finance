namespace BudgetWeb.Application.DTOs;

public record AjustementBudgetaireDto(
    long IdAjustement,
    string Reference,
    long IdPrevision,
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
    long IdExercice,
    short Annee,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    string TypeBudget,
    string LibelleLigne,
    decimal MontantAncien,
    decimal MontantNouveau,
    decimal Variation,
    string Motif,
    string Statut,
    long IdUtilisateurCreation,
    string NomUtilisateurCreation,
    DateTime DateCreation,
    long? IdUtilisateurValidation,
    string? NomUtilisateurValidation,
    DateTime? DateValidation);

public record AjustementHistoriqueLigneDto(
    long IdPrevision,
    string LibelleLigne,
    decimal MontantInitial,
    decimal MontantActuel,
    IReadOnlyList<AjustementBudgetaireDto> Ajustements);

public record AjustementLigneCandidateDto(
    long IdPrevision,
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
    long IdExercice,
    short Annee,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    string TypeBudget,
    string LibelleLigne,
    decimal MontantInitial,
    decimal MontantActuel,
    string CodeMode,
    int NbAjustementsValides,
    bool HasBrouillon,
    bool EstExerciceCourant);

public record CreateAjustementBudgetaireRequest(
    long IdPrevision,
    decimal MontantNouveau,
    string Motif);

public record UpdateAjustementBudgetaireRequest(
    decimal MontantNouveau,
    string Motif);

public record AjustementBudgetaireQuery(
    long? IdExercice = null,
    long? IdVersion = null,
    long? IdUB = null,
    string? Statut = null);
