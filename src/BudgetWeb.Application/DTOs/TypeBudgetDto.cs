namespace BudgetWeb.Application.DTOs;

public record TypeBudgetDto(long IdTypeBudget, string CodeType, string Libelle, int OrdreAffichage, bool Actif);
