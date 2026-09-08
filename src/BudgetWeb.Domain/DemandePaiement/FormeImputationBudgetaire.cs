namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Forme structurelle d'une imputation (champs renseignés), indépendamment de FK_TypeBudget.</summary>
public enum FormeImputationBudgetaire
{
    DepensesCourantes,
    ActionsExploitation,
    BudgetInvestissement
}
