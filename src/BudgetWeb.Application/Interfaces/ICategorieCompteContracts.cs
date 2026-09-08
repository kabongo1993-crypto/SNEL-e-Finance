using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface ICategorieCompteRepository
{
    Task<IReadOnlyList<CategorieCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<CategorieCompte?> GetByIdAsync(long idCategorieCompte, CancellationToken cancellationToken = default);
    Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default);
    Task<CategorieCompte> AddAsync(CategorieCompte entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICategorieCompteService
{
    Task<IReadOnlyList<CategorieCompteDto>> ListAsync(bool actifsSeulement = false, CancellationToken cancellationToken = default);
    Task<CategorieCompteDto?> GetByIdAsync(long idCategorieCompte, CancellationToken cancellationToken = default);
    Task<CategorieCompteDto> CreateAsync(CreateCategorieCompteRequest request, CancellationToken cancellationToken = default);
    Task<CategorieCompteDto?> UpdateAsync(long idCategorieCompte, UpdateCategorieCompteRequest request, CancellationToken cancellationToken = default);
}
