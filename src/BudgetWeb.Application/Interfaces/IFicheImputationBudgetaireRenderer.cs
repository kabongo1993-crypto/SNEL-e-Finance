using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

/// <summary>Rendu PDF de la fiche d'imputation budgétaire.</summary>
public interface IFicheImputationBudgetaireRenderer
{
    byte[] Render(FicheImputationBudgetaireDto payload);
}
