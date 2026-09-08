using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IBanqueRepository
{
    Task<IReadOnlyList<Banque>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<Banque?> GetByIdAsync(string idBanque, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string idBanque, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default);
    Task<Banque> AddAsync(Banque entity, CancellationToken cancellationToken = default);
    Task AddRangeInTransactionAsync(IReadOnlyList<Banque> entities, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IBanqueService
{
    Task<IReadOnlyList<BanqueDto>> ListAsync(bool actifsSeulement = false, CancellationToken cancellationToken = default);
    Task<BanqueDto?> GetByIdAsync(string idBanque, CancellationToken cancellationToken = default);
    Task<BanqueDto> CreateAsync(CreateBanqueRequest request, CancellationToken cancellationToken = default);
    Task<BanqueDto?> UpdateAsync(string idBanque, UpdateBanqueRequest request, CancellationToken cancellationToken = default);
    Task<ImportBanquesResultDto> ImportAsync(ImportBanquesRequest request, CancellationToken cancellationToken = default);
}
