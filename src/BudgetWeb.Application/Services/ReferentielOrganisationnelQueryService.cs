using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class ReferentielOrganisationnelQueryService : IReferentielOrganisationnelQueryService
{
    private readonly IReferentielOrganisationnelQueryRepository _repository;

    public ReferentielOrganisationnelQueryService(IReferentielOrganisationnelQueryRepository repository)
    {
        _repository = repository;
    }

    public Task<ReferentielOrganisationnelSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
        => _repository.GetSnapshotAsync(cancellationToken);
}
