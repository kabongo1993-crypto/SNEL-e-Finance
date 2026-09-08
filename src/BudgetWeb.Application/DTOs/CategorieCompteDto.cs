namespace BudgetWeb.Application.DTOs;

public record CategorieCompteDto(
    long IdCategorieCompte,
    string Libelle,
    string? Orientation,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateModification);

public record CreateCategorieCompteRequest(
    string Libelle,
    string? Orientation,
    bool Actif = true);

public record UpdateCategorieCompteRequest(
    string Libelle,
    string? Orientation,
    bool Actif);
