namespace BudgetWeb.Application.DTOs;

public record TypeBudgetDto(
    long IdTypeBudget,
    string CodeType,
    string Libelle,
    int OrdreAffichage,
    bool Actif,
    int NombrePrevisions);

public record CreateTypeBudgetRequest(
    string CodeType,
    string Libelle,
    int? OrdreAffichage,
    bool? Actif);

public record UpdateTypeBudgetRequest(
    string CodeType,
    string Libelle,
    int? OrdreAffichage,
    bool? Actif);
