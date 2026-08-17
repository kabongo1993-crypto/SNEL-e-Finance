using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class ReferentielService : IReferentielService
{
    private readonly IReferentielRepository _referentielRepository;

    public ReferentielService(IReferentielRepository referentielRepository)
    {
        _referentielRepository = referentielRepository;
    }

    public Task<IReadOnlyList<TypeBudgetDto>> GetTypesBudgetAsync(CancellationToken cancellationToken = default)
        => _referentielRepository.GetTypesBudgetAsync(cancellationToken);

    public Task<IReadOnlyList<ModePrevisionDto>> GetModesPrevisionAsync(CancellationToken cancellationToken = default)
        => _referentielRepository.GetModesPrevisionAsync(cancellationToken);
}
