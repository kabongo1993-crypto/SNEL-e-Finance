using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IReferentielOrganisationnelQueryRepository
{
    Task<ReferentielOrganisationnelSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface IReferentielOrganisationnelQueryService
{
    Task<ReferentielOrganisationnelSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
