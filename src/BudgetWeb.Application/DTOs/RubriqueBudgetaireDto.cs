namespace BudgetWeb.Application.DTOs;

public record RubriqueBudgetaireDto(
    long IdRB,
    string CodeRB,
    string Libelle,
    long? ParentId,
    string? ParentCode,
    string? ParentLibelle,
    int Niveau,
    bool Actif,
    DateTime DateCreation,
    int NombreEnfants,
    int NombrePrevisions,
    long? IdGroupeRB = null,
    string? CodeGroupe = null,
    string? LibelleGroupe = null,
    int? OrdreAffichageGroupe = null);

public record GroupeRubriqueBudgetaireDto(
    long IdGroupeRB,
    string CodeGroupe,
    string Libelle,
    int OrdreAffichage,
    bool Actif,
    DateTime DateCreation,
    int NombreRubriques);

public record RubriqueBudgetaireNoeudDto(
    long IdRB,
    string CodeRB,
    string Libelle,
    int Niveau,
    bool Actif,
    bool EstRupture,
    IReadOnlyList<RubriqueBudgetaireNoeudDto> Enfants);

public record CreateRubriqueBudgetaireRequest(
    string CodeRB,
    string Libelle,
    long? ParentId,
    bool? Actif);

public record UpdateRubriqueBudgetaireRequest(
    string CodeRB,
    string Libelle,
    long? ParentId,
    bool? Actif);

public record SetActifRequest(bool Actif);
