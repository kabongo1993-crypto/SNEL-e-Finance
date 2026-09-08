using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface ICompteFinancierRepository
{
    Task<IReadOnlyList<CompteFinancier>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<CompteFinancier?> GetByIdAsync(long idCompte, CancellationToken cancellationToken = default);
    Task<CompteFinancier?> GetTrackedByIdAsync(long idCompte, CancellationToken cancellationToken = default);
    Task<bool> ExistsNumeroAsync(string fkBanque, string numeroCompte, long? excludeId, CancellationToken cancellationToken = default);
    Task<bool> ExistsIdAsync(long idCompte, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<long>> ListIdsAsync(CancellationToken cancellationToken = default);
    Task<CompteFinancier> AddAsync(CompteFinancier entity, CancellationToken cancellationToken = default);
    Task AddRangeWithExplicitIdsAsync(IReadOnlyList<CompteFinancier> entities, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICompteFinancierService
{
    Task<IReadOnlyList<CompteFinancierDto>> ListAsync(bool actifsSeulement = false, CancellationToken cancellationToken = default);
    Task<CompteFinancierDto?> GetByIdAsync(long idCompte, CancellationToken cancellationToken = default);
    Task<CompteFinancierDto> CreateAsync(CreateCompteFinancierRequest request, CancellationToken cancellationToken = default);
    Task<CompteFinancierDto?> UpdateAsync(long idCompte, UpdateCompteFinancierRequest request, CancellationToken cancellationToken = default);
    Task<ImportComptesPreviewDto> PreviewImportAsync(ImportComptesPreviewRequest request, CancellationToken cancellationToken = default);
    Task<ImportComptesResultDto> ImportAsync(ImportComptesRequest request, CancellationToken cancellationToken = default);
}
