using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IGroupeRubriqueBudgetaireRepository
{
    Task<IReadOnlyList<GroupeRubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface IGroupeRubriqueBudgetaireService
{
    Task<IReadOnlyList<GroupeRubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
