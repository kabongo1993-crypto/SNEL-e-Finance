using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IDemandeurRepository
{
    Task<IReadOnlyList<Demandeur>> ListAsync(bool? actifOnly = true, CancellationToken cancellationToken = default);
    Task<Demandeur?> GetByIdAsync(long idDemandeur, CancellationToken cancellationToken = default);
    Task<Demandeur?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Demandeur?> GetTrackedAsync(long idDemandeur, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default);
    Task<UniteBudgetaire?> GetUniteBudgetaireAsync(long idUB, CancellationToken cancellationToken = default);
    Task<Demandeur> AddAsync(Demandeur entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDemandeurService
{
    Task<IReadOnlyList<DemandeurDto>> ListAsync(bool actifsSeulement = true, CancellationToken cancellationToken = default);
    Task<DemandeurDto?> GetByIdAsync(long idDemandeur, CancellationToken cancellationToken = default);
    Task<DemandeurDto> CreateAsync(CreateDemandeurRequest request, CancellationToken cancellationToken = default);
    Task<DemandeurDto?> UpdateAsync(long idDemandeur, UpdateDemandeurRequest request, CancellationToken cancellationToken = default);
}
