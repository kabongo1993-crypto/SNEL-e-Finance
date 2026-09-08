using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class GroupeRubriqueBudgetaireService : IGroupeRubriqueBudgetaireService
{
    private readonly IGroupeRubriqueBudgetaireRepository _repository;

    public GroupeRubriqueBudgetaireService(IGroupeRubriqueBudgetaireRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<GroupeRubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);
}
