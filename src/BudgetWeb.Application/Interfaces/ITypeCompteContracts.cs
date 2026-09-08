using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface ITypeCompteRepository
{
    Task<IReadOnlyList<TypeCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<TypeCompte?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, string? excludeCode, CancellationToken cancellationToken = default);
    Task<int> CountComptesByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<TypeCompte> AddAsync(TypeCompte entity, CancellationToken cancellationToken = default);
    Task RenameCodeAsync(string currentCode, string newCode, TypeCompte updated, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITypeCompteService
{
    Task<IReadOnlyList<TypeCompteDto>> ListAsync(bool actifsSeulement = false, CancellationToken cancellationToken = default);
    Task<TypeCompteDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<TypeCompteDto> CreateAsync(CreateTypeCompteRequest request, CancellationToken cancellationToken = default);
    Task<TypeCompteDto?> UpdateAsync(string code, UpdateTypeCompteRequest request, CancellationToken cancellationToken = default);
}
